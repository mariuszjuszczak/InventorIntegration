using Leap;

public class LeapEventListener : Listener
{
    ILeapEventDelegate eventDelegate;

    public LeapEventListener(ILeapEventDelegate delegateObject)
    {
        this.eventDelegate = delegateObject;
    }
    public override void OnInit(Controller controller)
    {
        this.eventDelegate.LeapEventNotification("onInit");
    }
    public override void OnConnect(Controller controller)
    {
        this.eventDelegate.LeapEventNotification("onConnect");
    }
    public override void OnFrame(Controller controller)
    {
        this.eventDelegate.LeapEventNotification("onFrame");
    }
    public override void OnExit(Controller controller)
    {
        this.eventDelegate.LeapEventNotification("onExit");
    }
    public override void OnDisconnect(Controller controller)
    {
        this.eventDelegate.LeapEventNotification("onDisconnect");
    }
}