using System.Runtime.CompilerServices;

namespace NativeStyles;

/// <summary>
/// Material destructive Text and Outlined buttons: an error-colored label (and outline) on a transparent container instead
/// of the error-filled container of the Destructive style. Used by the Android button mapping.
/// </summary>
internal static class DestructiveButtons
{
	static readonly ConditionalWeakTable<Button, object> s_attached = new();

	/// <summary>
	/// Adds the destructive triggers to a button, once. Trigger values sit above the style and local values (the
	/// destructive intent wins, as before) and are withdrawn exactly when a condition stops holding (not destructive, or
	/// another kind), which restores whatever the app or its style had set.
	/// </summary>
	public static void Attach(Button button)
	{
		if (s_attached.TryGetValue(button, out _))
			return;
		s_attached.Add(button, button);
		button.Triggers.Add(Create(ButtonKind.Text));
		button.Triggers.Add(Create(ButtonKind.Outlined));
	}

	static MultiTrigger Create(ButtonKind kind)
	{
		var trigger = new MultiTrigger(typeof(Button))
		{
			Conditions =
			{
				new PropertyCondition { Property = NativeButton.IsDestructiveProperty, Value = true },
				new PropertyCondition { Property = NativeButton.KindProperty, Value = kind },
			},
			Setters =
			{
				// Brand, dynamic and pinned colors included; the binding follows the app theme
				new Setter { Property = Button.TextColorProperty, Value = SystemColors.GetBinding(SystemColorRole.Destructive) },
				new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Transparent },
			},
		};
		if (kind == ButtonKind.Outlined)
			trigger.Setters.Add(new Setter { Property = Button.BorderColorProperty, Value = SystemColors.GetBinding(SystemColorRole.Destructive) });
		return trigger;
	}
}
