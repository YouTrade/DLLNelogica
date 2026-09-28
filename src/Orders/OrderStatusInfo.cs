namespace DLLNelogica.Orders;

internal enum OrderStatusClassification
{
    Unknown,
    Intermediate,
    Final
}

// Manual ProfitDLL 4.0.0.41, enum TConnectorOrderStatus (páginas 7–8).
// O manual fornece nomes/códigos, não uma tabela de transições. Estados ambíguos
// (DoneForDay, Replaced, Stopped, Calculated) permanecem não finais por precaução.
internal readonly record struct OrderStatusInfo(OrderStatusClassification Classification, string Description)
{
    internal string ClassificationDescription => Classification switch
    {
        OrderStatusClassification.Final => "final",
        OrderStatusClassification.Intermediate => "intermediário",
        _ => "desconhecido"
    };

    internal static OrderStatusInfo From(byte? code) => code switch
    {
        null => new(OrderStatusClassification.Unknown, "não confirmado"),
        <= 9 => FirstBlock(code.Value),
        <= 18 => SecondBlock(code.Value),
        >= 200 and <= 207 => ExtendedBlock(code.Value),
        _ => new(OrderStatusClassification.Unknown, "código fora do enum")
    };

    private static OrderStatusInfo FirstBlock(byte code) => code switch
    {
        0 => new(OrderStatusClassification.Intermediate, "aberta"),
        1 => new(OrderStatusClassification.Intermediate, "parcialmente executada"),
        2 => new(OrderStatusClassification.Final, "executada"),
        3 => new(OrderStatusClassification.Intermediate, "encerrada no dia; término definitivo não confirmado"),
        4 => new(OrderStatusClassification.Final, "cancelada"),
        5 => new(OrderStatusClassification.Intermediate, "substituída; término definitivo não confirmado"),
        6 => new(OrderStatusClassification.Intermediate, "cancelamento pendente"),
        7 => new(OrderStatusClassification.Intermediate, "parada; término definitivo não confirmado"),
        8 => new(OrderStatusClassification.Final, "rejeitada"),
        9 => new(OrderStatusClassification.Intermediate, "suspensa"),
        _ => new(OrderStatusClassification.Unknown, "código fora do enum")
    };

    private static OrderStatusInfo SecondBlock(byte code) => code switch
    {
        10 => new(OrderStatusClassification.Intermediate, "aceitação pendente"),
        11 => new(OrderStatusClassification.Intermediate, "calculada; término definitivo não confirmado"),
        12 => new(OrderStatusClassification.Final, "expirada"),
        13 => new(OrderStatusClassification.Intermediate, "aceita para negociação"),
        14 => new(OrderStatusClassification.Intermediate, "substituição pendente"),
        15 => new(OrderStatusClassification.Final, "parcialmente executada e cancelada"),
        16 => new(OrderStatusClassification.Intermediate, "recebida"),
        17 => new(OrderStatusClassification.Final, "parcialmente executada e expirada"),
        18 => new(OrderStatusClassification.Final, "parcialmente executada e rejeitada"),
        _ => new(OrderStatusClassification.Unknown, "código fora do enum")
    };

    // 204/205 descrevem encerramento; 200 é explicitamente desconhecido.
    // 201–203 e 206–207: inferência conservadora de etapas de criação/envio/espera,
    // pois o manual não garante término definitivo nesses estados.
    private static OrderStatusInfo ExtendedBlock(byte code) => code switch
    {
        200 => new(OrderStatusClassification.Unknown, "estado desconhecido informado pela DLL"),
        201 => new(OrderStatusClassification.Intermediate, "criada no Hades"),
        202 => new(OrderStatusClassification.Intermediate, "enviada à corretora"),
        203 => new(OrderStatusClassification.Intermediate, "criada no cliente"),
        204 => new(OrderStatusClassification.Final, "ordem não criada"),
        205 => new(OrderStatusClassification.Final, "cancelada pelo administrador"),
        206 => new(OrderStatusClassification.Intermediate, "atraso no gateway FIX"),
        207 => new(OrderStatusClassification.Intermediate, "ordem agendada"),
        _ => new(OrderStatusClassification.Unknown, "código fora do enum")
    };
}
