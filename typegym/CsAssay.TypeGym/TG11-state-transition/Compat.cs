namespace TypeGym;

public sealed record AccountState(int Balance);

public abstract record Transition
{
    private Transition() { }

    public sealed record Applied(AccountState State) : Transition;

    public sealed record Rejected(string Reason) : Transition;
}

public static class AccountTransitions
{
    public static Transition Withdraw(AccountState state, int amount) =>
        amount <= state.Balance
            ? new Transition.Applied(state with { Balance = state.Balance - amount })
            : new Transition.Rejected("insufficient-funds");
}

public static class Challenge
{
    public static string Probe() =>
        AccountTransitions.Withdraw(new AccountState(10), 3) switch
        {
            Transition.Applied applied => applied.State.Balance.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            Transition.Rejected rejected => rejected.Reason
        };
}
