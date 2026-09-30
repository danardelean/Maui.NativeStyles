using System.Diagnostics.CodeAnalysis;

namespace NativeStyles.Resources.Android;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The Shell flyout templates bind by name; the properties they read are kept by the DynamicDependency attributes on the constructor.")]
public partial class MaterialStyles : ResourceDictionary
{
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(BaseShellItem))]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, "Microsoft.Maui.Controls.MenuShellItem", "Microsoft.Maui.Controls")]
	public MaterialStyles() => InitializeComponent();
}
