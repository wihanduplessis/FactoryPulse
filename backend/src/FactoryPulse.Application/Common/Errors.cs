namespace FactoryPulse.Application.Common;

public static class Errors
{
    public static class Machine
    {
        public static readonly Error NotFound = Error.NotFound("Machine.NotFound", "The machine was not found.");
        public static readonly Error InvalidStatus = Error.Validation("Machine.InvalidStatus", "The provided machine status is not valid.");
    }

    public static class ProductionOrder
    {
        public static readonly Error NotFound = Error.NotFound("ProductionOrder.NotFound", "The production order was not found.");
        public static readonly Error DuplicateOrderNumber = Error.Conflict("ProductionOrder.DuplicateOrderNumber", "A production order with this order number already exists.");
        public static readonly Error MachineNotFound = Error.Validation("ProductionOrder.MachineNotFound", "The specified machine does not exist.");
        public static readonly Error MachineRetired = Error.Conflict("ProductionOrder.MachineRetired", "A production order cannot be assigned to a retired machine.");
        public static readonly Error InvalidTransition = Error.Conflict("ProductionOrder.InvalidTransition", "The requested status change is not allowed for this order.");
        public static readonly Error EndDateBeforeStart = Error.Validation("ProductionOrder.EndDateBeforeStart", "The end date cannot be before the start date.");
    }

    public static class Auth
    {
        public static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
        public static readonly Error EmailAlreadyExists = Error.Conflict("Auth.EmailAlreadyExists", "A user with this email already exists.");
        public static readonly Error InvalidRole = Error.Validation("Auth.InvalidRole", "The specified role is not valid.");
        public static readonly Error AccountLocked = Error.Unauthorized("Auth.AccountLocked", "The account is locked due to too many failed sign-in attempts. Try again later.");
    }
}
