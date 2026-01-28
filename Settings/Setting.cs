// Settings/Setting.cs

using System;

namespace ObsidianLauncher.Settings;

/// <summary>
///     Base class for a single setting value with type-safe access and change notifications.
/// </summary>
/// <typeparam name="T">The type of the setting value.</typeparam>
public class Setting<T>
{
    private T _value;
    private readonly T _defaultValue;
    private bool _isOverridden;

    /// <summary>
    ///     Occurs when the setting value changes.
    /// </summary>
    public event EventHandler<SettingChangedEventArgs<T>>? ValueChanged;

    /// <summary>
    ///     Gets or sets the setting value.
    /// </summary>
    public T Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                var oldValue = _value;
                _value = value;
                _isOverridden = true;
                OnValueChanged(oldValue, value);
            }
        }
    }

    /// <summary>
    ///     Gets the default value for this setting.
    /// </summary>
    public T DefaultValue => _defaultValue;

    /// <summary>
    ///     Gets whether this setting has been overridden from its default value.
    /// </summary>
    public bool IsOverridden => _isOverridden;

    /// <summary>
    ///     Gets the setting name/key.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the setting description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Creates a new setting with a default value.
    /// </summary>
    /// <param name="name">Setting name/key.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="description">Setting description.</param>
    public Setting(string name, T defaultValue, string description = "")
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
        _defaultValue = defaultValue;
        _value = defaultValue;
        _isOverridden = false;
    }

    /// <summary>
    ///     Resets the setting to its default value.
    /// </summary>
    public void Reset()
    {
        if (!Equals(_value, _defaultValue))
        {
            var oldValue = _value;
            _value = _defaultValue;
            _isOverridden = false;
            OnValueChanged(oldValue, _defaultValue);
        }
        else
        {
            _isOverridden = false;
        }
    }

    /// <summary>
    ///     Sets the value without marking it as overridden (used for loading from config).
    /// </summary>
    /// <param name="value">The value to set.</param>
    internal void SetValueSilently(T value)
    {
        _value = value;
        _isOverridden = !Equals(value, _defaultValue);
    }

    /// <summary>
    ///     Raises the ValueChanged event.
    /// </summary>
    protected virtual void OnValueChanged(T oldValue, T newValue)
    {
        ValueChanged?.Invoke(this, new SettingChangedEventArgs<T>(Name, oldValue, newValue));
    }

    /// <summary>
    ///     Converts the setting value to a string for serialization.
    /// </summary>
    public override string ToString()
    {
        return _value?.ToString() ?? string.Empty;
    }

    /// <summary>
    ///     Implicitly converts the Setting to its value type.
    /// </summary>
    public static implicit operator T(Setting<T> setting) => setting.Value;
}

/// <summary>
///     Event arguments for setting value changes.
/// </summary>
public class SettingChangedEventArgs<T> : EventArgs
{
    public string SettingName { get; }
    public T OldValue { get; }
    public T NewValue { get; }

    public SettingChangedEventArgs(string settingName, T oldValue, T newValue)
    {
        SettingName = settingName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
