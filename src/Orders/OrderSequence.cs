using DLLNelogica.Configuration;
using DLLNelogica.Interop;

namespace DLLNelogica.Orders;

internal sealed class OrderSequence(ProfitOrderGateway gateway, Action<string>? report = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null, TimeProvider? clock = null)
{
    private const int FinalObservationSeconds = 30;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly List<SequenceOrder> _orders = [];
    private readonly Action<string> _report = report ?? Console.WriteLine;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    internal async Task RunAsync(OrderSequenceOptions options, CancellationToken stopping)
    {
        if (!options.Enabled) { Write("Ordens: sequência desligada na configuração."); return; }
        try
        {
            if (!gateway.AccountExists(options, stopping))
            {
                Write("Ordens: conta inexistente na lista da DLL; nada enviado.");
                return;
            }
            var nativeTick = await gateway.PrepareAsync(options, stopping).ConfigureAwait(false);
            if (nativeTick != options.TickSize)
            {
                Write($"Ordens: TickSize={options.TickSize} difere do tick da DLL={nativeTick}; nada enviado.");
                return;
            }
            Write("Ordens: tick configurado conferido com os metadados da DLL.");
            Write("Ordens: sequência iniciada. Ctrl+C interrompe envios; ordens aceitas podem permanecer pendentes. Não há zeragem automática.");
            await StepAsync(options, 0, 1, false, options.BuyMarket, stopping).ConfigureAwait(false);
            await StepAsync(options, 1, 1, true, options.BuyLimit, stopping).ConfigureAwait(false);
            await StepAsync(options, 2, 2, false, options.SellMarket, stopping).ConfigureAwait(false);
            await StepAsync(options, 3, 2, true, options.SellLimit, stopping).ConfigureAwait(false);
            await WaitAsync(options.WaitSeconds[4], stopping).ConfigureAwait(false);
            if (!_orders.Any(order => order.Limit))
            {
                Write("Ordens: cancelamento dispensado; nenhuma ordem limitada foi enviada.");
            }
            Poll(stopping);
            CancelLimits(options, stopping);
            await ObserveUntilFinalAsync(options, stopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            Write("Ordens: encerramento solicitado; nenhum novo envio ou cancelamento automático. Verifique pendências no terminal.");
        }
        catch
        {
            // Nunca registrar mensagens nativas/exceções: podem conter a senha de roteamento.
            Write("Ordens: sequência interrompida por falha; Market Data e T&T continuam ativos. Verifique ordens no terminal.");
            try
            {
                Poll(stopping);
                CancelLimits(options, stopping);
                await ObserveUntilFinalAsync(options, stopping).ConfigureAwait(false);
            }
            catch
            {
                Write("Ordens: acompanhamento indisponível; estados finais não confirmados. Verifique o terminal.");
            }
        }
        finally
        {
            var net = _orders.Sum(order => order.Side == 1 ? (decimal)order.Filled : -(decimal)order.Filled);
            if (_orders.Count > 0 && _orders.All(order => !order.Status.HasValue))
            {
                Write("Ordens: saldo executado não confirmado; nenhuma leitura de estado teve sucesso. Não é possível concluir posição zero.");
            }
            else
            {
                Write($"Ordens: saldo executado observado nesta sequência={net}; posição anterior da conta não consultada.");
            }
            if (net != 0 || _orders.Any(order => !order.IsFinal))
            {
                Write("AVISO: possível posição aberta ou estado não confirmado; nenhuma zeragem automática.");
            }
            foreach (var order in _orders)
            {
                Write($"Ordens: id={order.Id} | estado={order.StatusInfo.ClassificationDescription} | código={order.Status} ({order.StatusInfo.Description}) | executada={(order.Status.HasValue ? order.Filled.ToString(System.Globalization.CultureInfo.InvariantCulture) : "não confirmada")} | leitura={order.ReadFailure ?? (order.Status.HasValue ? "disponível" : "não realizada")}");
            }
        }
    }

    private async Task ObserveUntilFinalAsync(OrderSequenceOptions options, CancellationToken stopping)
    {
        var started = _clock.GetTimestamp();
        while (_orders.Any(order => !order.IsFinal))
        {
            stopping.ThrowIfCancellationRequested();
            if (_orders.Where(order => !order.IsFinal).All(order => order.ReadDisabled))
            {
                Write("Ordens: acompanhamento encerrado; versão de leitura recusada. Estados finais não confirmados; verifique o terminal.");
                return;
            }
            var remaining = TimeSpan.FromSeconds(FinalObservationSeconds) - _clock.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero) { break; }
            await _delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), stopping).ConfigureAwait(false);
            stopping.ThrowIfCancellationRequested();
            if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(FinalObservationSeconds)) { break; }
            Poll(stopping);
            CancelLimits(options, stopping);
        }
        if (_orders.Any(order => !order.IsFinal))
        {
            Write($"Ordens: limite de acompanhamento final atingido ({FinalObservationSeconds}s); estados pendentes não confirmados. Verifique o terminal.");
        }
    }

    private async Task StepAsync(OrderSequenceOptions options, int step, byte side, bool limit, bool enabled,
        CancellationToken stopping)
    {
        await WaitAsync(options.WaitSeconds[step], stopping).ConfigureAwait(false);
        if (!enabled) { Write($"Ordens: passo {step + 1} desligado."); return; }
        var price = limit ? LimitPrice(options, side) : -1;
        stopping.ThrowIfCancellationRequested();
        Write($"Ordens: pedido passo={step + 1} | ativo={options.Ticker}:{options.Exchange} | lado={side} | limitada={limit} | quantidade={options.Quantity} | preço={price}");
        var id = gateway.Send(options, side, limit, price, stopping);
        Write($"Ordens: SendOrder retornou {id}.");
        if (id <= 0) { throw new InvalidOperationException("Envio recusado."); }
        _orders.Add(new SequenceOrder(id, side, limit));
        Poll(stopping);
    }

    private double LimitPrice(OrderSequenceOptions options, byte side)
    {
        var quote = gateway.LatestPrice($"{options.Ticker}:{options.Exchange}");
        if (quote is null || !double.IsFinite(quote.Value) || quote <= 0)
        {
            throw new InvalidOperationException("Cotação indisponível.");
        }
        var reference = (decimal)quote.Value;
        var raw = side == 1 ? reference - options.BuyLimitOffset : reference + options.SellLimitOffset;
        var ticks = raw / options.TickSize;
        var price = (side == 1 ? decimal.Floor(ticks) : decimal.Ceiling(ticks)) * options.TickSize;
        if (price <= 0) { throw new InvalidOperationException("Preço inválido."); }
        Write($"Ordens: referência={reference} | tick configurado={options.TickSize} | preço alinhado={price}.");
        return (double)price;
    }

    private async Task WaitAsync(int seconds, CancellationToken stopping)
    {
        Write($"Ordens: espera solicitada={seconds}s.");
        stopping.ThrowIfCancellationRequested();
        for (var elapsed = 0; elapsed < seconds; elapsed++)
        {
            await _delay(TimeSpan.FromSeconds(1), stopping).ConfigureAwait(false);
            Poll(stopping);
        }
    }

    private void Poll(CancellationToken stopping)
    {
        foreach (var order in _orders.Where(order => !order.IsFinal && !order.ReadDisabled))
        {
            (int Result, byte Status, long Filled) result;
            try { result = gateway.Read(order.Id, stopping); }
            catch (OperationCanceledException) { throw; }
            catch
            {
                ReportReadFailure(order, "consulta indisponível; estado não confirmado");
                continue;
            }
            if (result.Result != 0)
            {
                order.ReadDisabled = result.Result == (int)NResult.NL_VERSION_NOT_SUPPORTED;
                var reason = order.ReadDisabled
                    ? $"NL_VERSION_NOT_SUPPORTED ({result.Result}) em GetOrderDetails V0; consultas suspensas nesta sessão"
                    : $"GetOrderDetails retornou {result.Result}; estado ainda não confirmado";
                ReportReadFailure(order, reason);
                continue;
            }
            if (order.ReadFailure is not null || order.Status != result.Status || order.Filled != result.Filled)
            {
                var status = OrderStatusInfo.From(result.Status);
                Write($"Ordens: id={order.Id} | estado={result.Status} ({status.Description}) | classificação={status.ClassificationDescription} | executada={result.Filled}.");
            }
            order.ReadFailure = null;
            order.Status = result.Status;
            order.Filled = result.Filled;
        }
    }

    private void CancelLimits(OrderSequenceOptions options, CancellationToken stopping)
    {
        foreach (var order in _orders.Where(order => order.Limit))
        {
            if (order.IsFinal)
            {
                if (!order.CancelRequested) { ReportCancellation(order, "já final; cancelamento dispensado"); }
                continue;
            }
            if (order.CancelAccepted) { continue; }
            try
            {
                order.CancelRequested = true;
                var result = gateway.Cancel(options, order.Id, stopping);
                order.CancelAccepted = result == 0;
                ReportCancellation(order, $"SendCancelOrderV2 retornou {result}; aguardando estado final");
            }
            catch (OperationCanceledException) { throw; }
            catch { ReportCancellation(order, "cancelamento indisponível; será reconsultado"); }
        }
    }

    private void ReportReadFailure(SequenceOrder order, string failure)
    {
        if (order.ReadFailure != failure)
        {
            Write($"Ordens: id={order.Id} | leitura={failure}.");
            order.ReadFailure = failure;
        }
    }

    private void ReportCancellation(SequenceOrder order, string message)
    {
        if (order.LastCancellationReport != message)
        {
            Write($"Ordens: id={order.Id} | {message}.");
            order.LastCancellationReport = message;
        }
    }

    private void Write(string text)
    {
        try { _report(text); }
        catch { /* Falha do relatório não interrompe os feeds. */ }
    }
}
