namespace FactoryPulse.Application.Common;

public static class Errors
{
    public static class Machine
    {
        public static readonly Error NotFound = Error.NotFound("Machine.NotFound", "The machine was not found.");

        public static readonly Error InvalidStatus = Error.Validation("Machine.InvalidStatus", "The provided machine status is not valid.");
    }
}
