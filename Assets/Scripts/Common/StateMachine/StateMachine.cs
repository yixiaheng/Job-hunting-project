using System.Collections.Generic;
using UnityEngine;
using System;

public class StateMachine
{
    private readonly Dictionary<Type, IState> states = new();
    public IState currentState{ get; private set; }
    public void Register<T>(T state) where T : IState
    {
        if (state == null)
        {
            Debug.LogError("不能注册空 State");
            return;
        }

        states.Add(typeof(T), state);
    }


    public void ChangeState<T>() where T : class , IState
    {
        IState State = GetState<T>();

        currentState?.Exit();
        currentState = State;
        currentState?.Enter();
    }

    public T GetState<T>() where T : class, IState
    {
        if (!states.TryGetValue(typeof(T), out IState state))
        {
            Debug.LogError($"State 尚未注册：{typeof(T).Name}");
            return null;
        }

        return state as T;
    }
    public void Update()
    {
        currentState?.Update();
    }

    public void PhysicsUpdate()
    {
        currentState?.PhysicsUpdate();
    }

    public void Exit()
    {
        currentState?.Exit();
        currentState = null;
    }
}
