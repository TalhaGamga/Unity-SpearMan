public class RifleInputHandler : IInputHandler
{
    private IRifleCombat _rifleCombat;
    private IHumanoidCombatPromptReceiver _promptReceiver;

    public RifleInputHandler(IRifleCombat rifleCombat, IHumanoidCombatPromptReceiver promptReceiver)
    {
        _rifleCombat = rifleCombat;
        _promptReceiver = promptReceiver;
    }

    public void BindInputs()
    {
        _promptReceiver.OnPrimaryCombatInput += _rifleCombat.AttemptFire;
        _promptReceiver.OnPrimaryCombatCancel += _rifleCombat.StopFiring;
        _promptReceiver.OnReloadInput += _rifleCombat.Reload;
        _promptReceiver.OnAimInput += _rifleCombat.TakeAim;
    }

    public void UnbindInputs()
    {
        _promptReceiver.OnPrimaryCombatInput -= _rifleCombat.AttemptFire;
        _promptReceiver.OnPrimaryCombatCancel -= _rifleCombat.StopFiring;
        _promptReceiver.OnReloadInput -= _rifleCombat.Reload;
        _promptReceiver.OnAimInput -= _rifleCombat.TakeAim;
    }
}