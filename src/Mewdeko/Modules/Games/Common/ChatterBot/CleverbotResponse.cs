namespace Mewdeko.Modules.Games.Common.ChatterBot;

public class CleverbotResponse
{
    public string Cs { get; set; }
    public string Output { get; set; }
}

public class CleverbotIoCreateResponse
{
    public string Status { get; set; }
    public string Nick { get; set; }
}

public class CleverbotIoAskResponse
{
    public string Status { get; set; }
    public string Response { get; set; }
}