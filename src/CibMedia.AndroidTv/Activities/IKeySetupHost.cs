namespace CibMedia.AndroidTv.Activities;

// What happens once the keys are stored differs between first run and Settings; the setup
// screen itself does not need to know which it is.
public interface IKeySetupHost
{
    void OnKeysSaved();
}
