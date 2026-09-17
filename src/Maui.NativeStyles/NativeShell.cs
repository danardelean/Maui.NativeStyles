namespace NativeStyles;

public enum TabBarMinimizeBehavior
{
	/// <summary>System default.</summary>
	Automatic,
	Never,
	/// <summary>iOS 26: the floating tab bar collapses while scrolling down and expands when scrolling up.</summary>
	OnScrollDown,
	OnScrollUp,
}

/// <summary>Shell-level native options.</summary>
public static class NativeShell
{
	/// <summary>iOS 26 <c>UITabBarController.tabBarMinimizeBehavior</c>. No effect on Android or on iOS 15–18.</summary>
	public static readonly BindableProperty TabBarMinimizeBehaviorProperty = BindableProperty.CreateAttached(
		"TabBarMinimizeBehavior", typeof(TabBarMinimizeBehavior), typeof(NativeShell), TabBarMinimizeBehavior.Automatic,
		propertyChanged: (b, _, _) => NativeStylesExtensions.Refresh(b));

	public static TabBarMinimizeBehavior GetTabBarMinimizeBehavior(BindableObject shell) => (TabBarMinimizeBehavior)shell.GetValue(TabBarMinimizeBehaviorProperty);
	public static void SetTabBarMinimizeBehavior(BindableObject shell, TabBarMinimizeBehavior value) => shell.SetValue(TabBarMinimizeBehaviorProperty, value);
}
