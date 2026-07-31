namespace TypeGym;

public sealed class Account
{
    public int Balance { get; set; }

    public void Withdraw(int amount)
    {
        Balance -= amount;
    }
}

public static class Challenge
{
    public static string Probe()
    {
        var account = new Account { Balance = 10 };
        account.Withdraw(3);
        return account.Balance.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
