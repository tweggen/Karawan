using System.Numerics;
using engine.news;

namespace builtin.controllers;

public interface IFingerState
{
    public void HandleMotion(Event ev);
    public void HandleReleased(Event ev);
    public void HandlePressed(Event ev);

}