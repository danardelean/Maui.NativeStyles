using Xunit.Abstractions;

[assembly: TestCollectionOrderer("NativeStyles.DeviceTests.ButtonTestsLast", "Maui.NativeStyles.DeviceTests")]

namespace NativeStyles.DeviceTests;

/// <summary>
/// iOS 26.5 simulator: after the library has applied UIButtonConfigurations to buttons that were never on screen, the
/// next UIKit view that reads Swift Observation state (a UIToolbar, a view moving to a window) intermittently crashes
/// the app with EXC_BAD_ACCESS in ObservationTracking._AccessList.addAccess (see CONTRIBUTING.md, device tests).
/// Running the button tests last keeps every other result deterministic; classes are otherwise ordered by name.
/// </summary>
public class ButtonTestsLast : ITestCollectionOrderer
{
	public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
		testCollections
			.OrderBy(collection => collection.DisplayName.Contains("ButtonTests", StringComparison.Ordinal) ? 1 : 0)
			.ThenBy(collection => collection.DisplayName, StringComparer.Ordinal);
}
