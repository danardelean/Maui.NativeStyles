global using Xunit;
using Microsoft.Maui.Platform;

// Every test drives UIKit / Android views on the one UI thread: run them one after the other
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NativeStyles.DeviceTests;

/// <summary>
/// Tests run on a worker thread: native views are created and inspected on the UI thread, with the running window's
/// MauiContext (the activity on Android, so Material theme attributes resolve as in an app).
/// </summary>
static class UI
{
	static List<IElement>? s_created;

	/// <summary>Runs on the UI thread; handlers created through <see cref="PlatformView"/> are disconnected afterwards, as when a page goes away.</summary>
	public static Task<T> Run<T>(Func<T> action) => MainThread.InvokeOnMainThreadAsync(() =>
	{
		var created = s_created = [];
		try
		{
			return action();
		}
		finally
		{
			s_created = null;
			foreach (var element in created)
			{
#if IOS
				(element.Handler?.PlatformView as UIKit.UIView)?.RemoveFromSuperview();
#endif
				element.Handler?.DisconnectHandler();
			}
		}
	});

	public static IMauiContext Context =>
		Application.Current?.Windows.FirstOrDefault()?.Handler?.MauiContext
		?? throw new InvalidOperationException("The test runner window has no handler yet.");

	/// <summary>Creates the handler registered for the view (with every mapping applied) and returns its native view.</summary>
	public static TPlatformView PlatformView<TPlatformView>(IView view) where TPlatformView : class
	{
		var handler = view.ToHandler(Context);
		s_created?.Add(view);
		return handler.PlatformView as TPlatformView
			?? throw new InvalidOperationException($"{view.GetType().Name} is not rendered as a {typeof(TPlatformView).Name}.");
	}

	public static IEnumerable<object[]> Roles() => Enum.GetValues<SystemColorRole>().Select(role => new object[] { role });
}
