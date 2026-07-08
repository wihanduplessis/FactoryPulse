namespace FactoryPulse.Domain.Exceptions;

public class InvalidProductionOrderTransitionException : Exception
{
    public InvalidProductionOrderTransitionException(string message)
        : base(message)
    {
    }
}
