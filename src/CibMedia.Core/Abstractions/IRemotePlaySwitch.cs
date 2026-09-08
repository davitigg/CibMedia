namespace CibMedia.Core.Abstractions;

// Whether this box serves its playback endpoints at all. Off until someone turns it on.
public interface IRemotePlaySwitch
{
    bool IsOn { get; set; }

    event Action? Changed;
}
