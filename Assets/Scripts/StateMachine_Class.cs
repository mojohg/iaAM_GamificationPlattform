using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class State
{

    public virtual void OnEnter()
    {
        // Code, der beim Betreten des Zustands ausgeführt wird
    }

    public virtual void OnUpdate()
    {
        // Code, der im aktuellen Zustand während des Updates ausgeführt wird
    }

    public virtual void OnExit()
    {
        // Code, der beim Verlassen des Zustands ausgeführt wird
    }
    
}

public class StateMachine_Class : MonoBehaviour
{
    private State currentState;

    public void ChangeState(State newState)
    {
        // Beim Wechsel des Zustands vorherigen Zustand verlassen
        if (currentState != null)
        {
            currentState.OnExit();
        }

        // Neuen Zustand setzen und Betreten
        currentState = newState;
        currentState.OnEnter();
    }

    public void Update()
    {
        // Im aktuellen Zustand aktualisieren
        if (currentState != null)
        {
            currentState.OnUpdate();
        }
    }
}
