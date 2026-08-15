using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using engine.inputs;

namespace builtin.controllers.bindings;

/**
 * The live control -> action table (WP-6.4).
 *
 * THREE PROPERTIES THIS HAS TO HAVE, AND WHY EACH SHAPES THE CODE
 *
 * 1. It is LIVE STATE, not load-time config. Rebinding writes one entry; it does not
 *    reload a file. So every accessor is lock-guarded and the reverse index is rebuilt
 *    on write rather than assumed static.
 *
 * 2. A write must be PERSISTABLE, in the same schema it was read from. ToJson/FromJson
 *    are exact inverses, which is what lets a user's overrides survive a game update:
 *    the shipped file and the user's file are the same shape, so one can be layered
 *    over the other.
 *
 * 3. Rebinding needs "listen for the NEXT control". That is BeginCapture/EndCapture
 *    here - a mode on the table, not a separate subsystem, because the thing that must
 *    not translate the event is the thing that would otherwise translate it.
 *
 * Lookup is control -> action and must be fast: it runs on every key event. The reverse
 * index is maintained on write, so the read path is one dictionary probe.
 */
public class BindingTable
{
    private readonly object _lo = new();

    private readonly SortedDictionary<string, ActionBinding> _mapActions = new();

    /**
     * Reverse index, rebuilt on every mutation. Several controls map to one action;
     * one control maps to at most one action - a control bound twice is a
     * configuration error, and the later binding wins with a trace rather than
     * silently firing both.
     */
    private readonly Dictionary<Control, string> _mapControlToAction = new();

    private bool _isCapturing;


    /**
     * A snapshot. Callers iterate this on the logical thread while a rebinding UI may
     * be writing, so handing out the live dictionary would be a race.
     */
    public IReadOnlyList<ActionBinding> Actions
    {
        get
        {
            lock (_lo)
            {
                return _mapActions.Values.ToList();
            }
        }
    }


    public ActionBinding? Find(string action)
    {
        lock (_lo)
        {
            return _mapActions.TryGetValue(action, out var binding) ? binding : null;
        }
    }


    /**
     * The hot path: which action, if any, does this control drive?
     */
    public string? ActionOf(Control control)
    {
        lock (_lo)
        {
            return _mapControlToAction.TryGetValue(control, out var action) ? action : null;
        }
    }


    public void Set(ActionBinding binding)
    {
        lock (_lo)
        {
            _mapActions[binding.Action] = binding;
            _reindex();
        }
    }


    /**
     * Bind one control to one action, replacing whatever else that control drove.
     *
     * This is what a rebinding UI calls, and the "replacing" half is the part worth
     * stating: a control that already drove another action is REMOVED from it. Leaving
     * it bound to both is never what the user meant when they pressed a key on a
     * "bind this" prompt, and it produces two actions from one press.
     */
    public void Bind(string action, Control control)
    {
        lock (_lo)
        {
            foreach (var other in _mapActions.Values)
            {
                other.Controls.Remove(control);
            }

            if (!_mapActions.TryGetValue(action, out var binding))
            {
                binding = new ActionBinding { Action = action };
                _mapActions[action] = binding;
            }

            if (!binding.Controls.Contains(control))
            {
                binding.Controls.Add(control);
            }

            _reindex();
        }
    }


    public void Unbind(string action, Control control)
    {
        lock (_lo)
        {
            if (_mapActions.TryGetValue(action, out var binding))
            {
                binding.Controls.Remove(control);
                _reindex();
            }
        }
    }


    public void Clear()
    {
        lock (_lo)
        {
            _mapActions.Clear();
            _reindex();
        }
    }


    private void _reindex()
    {
        _mapControlToAction.Clear();
        foreach (var binding in _mapActions.Values)
        {
            foreach (var control in binding.Controls)
            {
                _mapControlToAction[control] = binding.Action;
            }
        }
    }


    #region Capture

    /**
     * Enter capture mode. While capturing, the input path must deliver raw controls to
     * the rebinding UI instead of translating them to actions - otherwise pressing
     * "Escape" to bind it would also close the menu.
     */
    public void BeginCapture()
    {
        lock (_lo)
        {
            _isCapturing = true;
        }
    }


    public void EndCapture()
    {
        lock (_lo)
        {
            _isCapturing = false;
        }
    }


    public bool IsCapturing
    {
        get
        {
            lock (_lo)
            {
                return _isCapturing;
            }
        }
    }

    #endregion


    #region Persistence

    /**
     * The persisted shape. Deliberately flat and hand-editable:
     *
     *   {
     *     "interact": {
     *       "description": "Interact with your environment.",
     *       "controls": [ "Key:E", "GamepadButton:Y" ]
     *     },
     *     "steer": {
     *       "controls": [ "GamepadTrigger:0" ],
     *       "modifiers": [ "deadzone 0.15", "curve 4" ]
     *     }
     *   }
     */
    public JsonObject ToJson()
    {
        lock (_lo)
        {
            var root = new JsonObject();
            foreach (var binding in _mapActions.Values)
            {
                var obj = new JsonObject();
                if (!string.IsNullOrEmpty(binding.Description))
                {
                    obj["description"] = binding.Description;
                }

                var controls = new JsonArray();
                foreach (var control in binding.Controls)
                {
                    controls.Add(control.ToString());
                }

                obj["controls"] = controls;

                if (binding.Modifiers.Count > 0)
                {
                    var modifiers = new JsonArray();
                    foreach (var m in binding.Modifiers)
                    {
                        modifiers.Add(InputModifiers.ToSpec(m));
                    }

                    obj["modifiers"] = modifiers;
                }

                root[binding.Action] = obj;
            }

            return root;
        }
    }


    public string ToJsonString()
        => ToJson().ToJsonString(new JsonSerializerOptions { WriteIndented = true });


    /**
     * Replace the whole table from JSON.
     *
     * Unparseable entries are SKIPPED rather than throwing, and the caller is told how
     * many via the return value. A binding file is user-editable and may also come from
     * an older version of the game; one bad line must not cost the player every other
     * binding they have.
     */
    public int FromJson(JsonNode? node)
    {
        int nSkipped = 0;

        lock (_lo)
        {
            _mapActions.Clear();

            if (node is JsonObject root)
            {
                foreach (var kvp in root)
                {
                    if (kvp.Value is not JsonObject obj)
                    {
                        ++nSkipped;
                        continue;
                    }

                    var binding = new ActionBinding
                    {
                        Action = kvp.Key,
                        Description = obj["description"]?.GetValue<string>(),
                    };

                    if (obj["controls"] is JsonArray controls)
                    {
                        foreach (var c in controls)
                        {
                            if (Control.TryParse(c?.GetValue<string>(), out var control))
                            {
                                binding.Controls.Add(control);
                            }
                            else
                            {
                                ++nSkipped;
                            }
                        }
                    }

                    if (obj["modifiers"] is JsonArray modifiers)
                    {
                        foreach (var m in modifiers)
                        {
                            var modifier = InputModifiers.TryParse(m?.GetValue<string>());
                            if (null != modifier)
                            {
                                binding.Modifiers.Add(modifier);
                            }
                            else
                            {
                                ++nSkipped;
                            }
                        }
                    }

                    _mapActions[binding.Action] = binding;
                }
            }

            _reindex();
        }

        return nSkipped;
    }


    public int FromJsonString(string json) => FromJson(JsonNode.Parse(json));

    #endregion
}
