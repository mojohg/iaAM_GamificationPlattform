using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Verwendung der Zustandsmaschine
public class Statemachine : MonoBehaviour
{
    private StateMachine_Class stateMachine;
    [SerializeField] private StateMachine_Class stateMachine_Class;
    [SerializeField] private Animator animator;


    public static Statemachine Instance { get; private set; }

    public Statemachine(Animator animator)
    {
        stateMachine_Class = new StateMachine_Class();
        stateMachine_Class.ChangeState(new InitState(animator));
    }
    public void ChangeToInitState()
    {
        stateMachine_Class.ChangeState(new InitState(animator));
    }
     public void ChangeToIdleState()
    {
        stateMachine_Class.ChangeState(new IdleState(animator));
    }
    
    



    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void Start()
    {
       Debug.Log("START");

        stateMachine_Class.ChangeState(new InitState(animator)); // Starte im Hauptmenü
    }
    public void Update()
    {
        stateMachine_Class.Update();
        
    }
}

public class InitState : State
{

    private Animator animator;
    public InitState(Animator animator)
    {

        this.animator = animator;

    }
    public override void OnEnter()
    {
        Debug.Log("Init");

    }

    public override void OnUpdate()
    {
    }


    public override void OnExit()
    {


    }
}
public class IdleState : State
{
    private Animator animator;  

    public IdleState(Animator animator)
    {
        this.animator = animator;
    }
    public override void OnEnter()
    {
        Debug.Log("IDLE");
    
    }

    public override void OnUpdate()
    {

    }


    public override void OnExit()
    {


    }
}




