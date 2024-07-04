public interface IObserver
{
    void OnNotify(string eventType, object data);
}

public interface ISubject
{
    void RegisterObserver(string topic, IObserver observer);
    void RemoveObserver(string topic, IObserver observer);
    void NotifyObservers(string topic, object data);
}
