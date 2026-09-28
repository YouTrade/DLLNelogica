using DLLNelogica.Orders;

namespace DLLNelogica.Tests;

public sealed class OrderStatusTests
{
    [Theory]
    [InlineData(0, false, "aberta")]
    [InlineData(1, false, "parcialmente executada")]
    [InlineData(2, true, "executada")]
    [InlineData(3, false, "encerrada no dia")]
    [InlineData(4, true, "cancelada")]
    [InlineData(5, false, "substituída")]
    [InlineData(6, false, "cancelamento pendente")]
    [InlineData(7, false, "parada")]
    [InlineData(8, true, "rejeitada")]
    [InlineData(9, false, "suspensa")]
    [InlineData(10, false, "aceitação pendente")]
    [InlineData(11, false, "calculada")]
    [InlineData(12, true, "expirada")]
    [InlineData(13, false, "aceita para negociação")]
    [InlineData(14, false, "substituição pendente")]
    [InlineData(15, true, "parcialmente executada e cancelada")]
    [InlineData(16, false, "recebida")]
    [InlineData(17, true, "parcialmente executada e expirada")]
    [InlineData(18, true, "parcialmente executada e rejeitada")]
    [InlineData(201, false, "criada no Hades")]
    [InlineData(202, false, "enviada à corretora")]
    [InlineData(203, false, "criada no cliente")]
    [InlineData(204, true, "ordem não criada")]
    [InlineData(205, true, "cancelada pelo administrador")]
    [InlineData(206, false, "atraso no gateway FIX")]
    [InlineData(207, false, "ordem agendada")]
    public void KnownStatesHaveExplicitDescriptionsAndConservativeClassification(byte code, bool final, string description)
    {
        var order = new SequenceOrder(1, 1, true) { Status = code };
        Assert.Equal(final, order.IsFinal);
        Assert.Equal(final ? OrderStatusClassification.Final : OrderStatusClassification.Intermediate,
            order.StatusInfo.Classification);
        Assert.StartsWith(description, order.StatusInfo.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((byte)19)]
    [InlineData((byte)199)]
    [InlineData((byte)200)]
    [InlineData((byte)208)]
    [InlineData((byte)255)]
    public void UnconfirmedUnknownAndUnmappedStatesAreNeverFinal(byte? code)
    {
        var order = new SequenceOrder(1, 1, true) { Status = code };
        Assert.False(order.IsFinal);
        Assert.Equal(code, order.Status);
        Assert.Equal(OrderStatusClassification.Unknown, order.StatusInfo.Classification);
        Assert.Equal("desconhecido", order.StatusInfo.ClassificationDescription);
    }
}
