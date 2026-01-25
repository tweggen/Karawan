using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aihao.Models;

namespace Aihao.Services;

/// <summary>
/// Service for managing and executing actions.
/// Actions are decoupled from their triggers (menus, keybindings, buttons).
/// </summary>
public class ActionService
{
    private readonly Dictionary<string, ActionDefinition> _actions = new();
    private readonly Dictionary<string, Func<Task>> _handlers = new();
    private readonly Dictionary<string, Func<bool>> _canExecuteHandlers = new();
    private readonly Dictionary<string, KeyBinding> _effectiveBindings = new();
    private List<KeyBindingOverride> _userOverrides = new();
    
    /// <summary>
    /// Event raised when an action is executed.
    /// </summary>
    public event EventHandler<ActionExecutedEventArgs>? ActionExecuted;
    
    /// <summary>
    /// Event raised when keybindings change.
    /// </summary>
    public event EventHandler? KeyBindingsChanged;
    
    /// <summary>
    /// All registered actions.
    /// </summary>
    public IReadOnlyDictionary<string, ActionDefinition> Actions => _actions;
    
    /// <summary>
    /// Current effective keybindings (after applying user overrides).
    /// </summary>
    public IReadOnlyDictionary<string, KeyBinding> EffectiveBindings => _effectiveBindings;
    
    /// <summary>
    /// Register an action.
    /// </summary>
    public void RegisterAction(ActionDefinition action, Func<Task> handler, Func<bool>? canExecute = null)
    {
        _actions[action.Id] = action;
        _handlers[action.Id] = handler;
        if (canExecute != null)
        {
            _canExecuteHandlers[action.Id] = canExecute;
        }
        
        // Set up default keybinding
        if (action.DefaultKeyBinding != null)
        {
            _effectiveBindings[action.Id] = action.DefaultKeyBinding;
        }
    }
    
    /// <summary>
    /// Register an action with a synchronous handler.
    /// </summary>
    public void RegisterAction(ActionDefinition action, Action handler, Func<bool>? canExecute = null)
    {
        RegisterAction(action, () => { handler(); return Task.CompletedTask; }, canExecute);
    }
    
    /// <summary>
    /// Execute an action by ID.
    /// </summary>
    public async Task<bool> ExecuteAsync(string actionId)
    {
        if (!_handlers.TryGetValue(actionId, out var handler))
        {
            return false;
        }
        
        if (!CanExecute(actionId))
        {
            return false;
        }
        
        try
        {
            await handler();
            ActionExecuted?.Invoke(this, new ActionExecutedEventArgs(actionId));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Action '{actionId}' failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Check if an action can be executed.
    /// </summary>
    public bool CanExecute(string actionId)
    {
        if (!_actions.TryGetValue(actionId, out var action))
        {
            return false;
        }
        
        if (_canExecuteHandlers.TryGetValue(actionId, out var canExecute))
        {
            if (!canExecute())
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Get the effective keybinding for an action.
    /// </summary>
    public KeyBinding? GetKeyBinding(string actionId)
    {
        _effectiveBindings.TryGetValue(actionId, out var binding);
        return binding;
    }
    
    /// <summary>
    /// Find the action bound to a key combination.
    /// </summary>
    public string? FindActionForKeyBinding(KeyBinding binding)
    {
        foreach (var kvp in _effectiveBindings)
        {
            if (kvp.Value.Equals(binding))
            {
                return kvp.Key;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Apply user keybinding overrides.
    /// </summary>
    public void ApplyOverrides(List<KeyBindingOverride> overrides)
    {
        _userOverrides = overrides ?? new List<KeyBindingOverride>();
        RebuildEffectiveBindings();
        KeyBindingsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Set a custom keybinding for an action.
    /// </summary>
    public void SetKeyBinding(string actionId, KeyBinding? binding)
    {
        // Remove existing override for this action
        _userOverrides.RemoveAll(o => o.ActionId == actionId);
        
        if (binding == null)
        {
            // Disable the keybinding
            _userOverrides.Add(new KeyBindingOverride
            {
                ActionId = actionId,
                Disabled = true
            });
        }
        else
        {
            // Set custom binding
            _userOverrides.Add(new KeyBindingOverride
            {
                ActionId = actionId,
                KeyBinding = binding.ToGestureString()
            });
        }
        
        RebuildEffectiveBindings();
        KeyBindingsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Reset a keybinding to its default.
    /// </summary>
    public void ResetKeyBinding(string actionId)
    {
        _userOverrides.RemoveAll(o => o.ActionId == actionId);
        RebuildEffectiveBindings();
        KeyBindingsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Reset all keybindings to defaults.
    /// </summary>
    public void ResetAllKeyBindings()
    {
        _userOverrides.Clear();
        RebuildEffectiveBindings();
        KeyBindingsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Get current user overrides for saving.
    /// </summary>
    public List<KeyBindingOverride> GetOverrides() => _userOverrides.ToList();
    
    /// <summary>
    /// Get all actions in a category.
    /// </summary>
    public IEnumerable<ActionDefinition> GetActionsByCategory(string category)
    {
        return _actions.Values.Where(a => a.Category == category);
    }
    
    /// <summary>
    /// Get all categories.
    /// </summary>
    public IEnumerable<string> GetCategories()
    {
        return _actions.Values.Select(a => a.Category).Distinct().OrderBy(c => c);
    }
    
    /// <summary>
    /// Search actions by name or ID.
    /// </summary>
    public IEnumerable<ActionDefinition> SearchActions(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _actions.Values.Where(a => a.ShowInCommandPalette);
        }
        
        var lowerQuery = query.ToLowerInvariant();
        return _actions.Values
            .Where(a => a.ShowInCommandPalette &&
                       (a.DisplayName.ToLowerInvariant().Contains(lowerQuery) ||
                        a.Id.ToLowerInvariant().Contains(lowerQuery) ||
                        a.Category.ToLowerInvariant().Contains(lowerQuery)))
            .OrderBy(a => a.DisplayName);
    }
    
    private void RebuildEffectiveBindings()
    {
        _effectiveBindings.Clear();
        
        // Start with defaults
        foreach (var action in _actions.Values)
        {
            if (action.DefaultKeyBinding != null)
            {
                _effectiveBindings[action.Id] = action.DefaultKeyBinding;
            }
        }
        
        // Apply overrides
        foreach (var ovr in _userOverrides)
        {
            if (ovr.Disabled)
            {
                _effectiveBindings.Remove(ovr.ActionId);
            }
            else if (!string.IsNullOrEmpty(ovr.KeyBinding))
            {
                var binding = KeyBinding.Parse(ovr.KeyBinding);
                if (binding != null)
                {
                    _effectiveBindings[ovr.ActionId] = binding;
                }
            }
        }
    }
}

/// <summary>
/// Event args for action execution.
/// </summary>
public class ActionExecutedEventArgs : EventArgs
{
    public string ActionId { get; }
    
    public ActionExecutedEventArgs(string actionId)
    {
        ActionId = actionId;
    }
}
