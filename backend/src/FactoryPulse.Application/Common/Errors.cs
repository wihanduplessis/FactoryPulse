namespace FactoryPulse.Application.Common;

public static class Errors
{
    public static class Machine
    {
        public static readonly Error NotFound = Error.NotFound("Machine.NotFound", "The machine was not found.");
    }
}
