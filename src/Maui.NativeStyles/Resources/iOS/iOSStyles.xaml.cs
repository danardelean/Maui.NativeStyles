using System.Diagnostics.CodeAnalysis;

namespace NativeStyles.Resources.iOS;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The Shell flyout templates bind by name and the RadioButton template uses TemplateBinding; the properties they read are kept by the DynamicDependency attributes on the constructor.")]
public partial class iOSStyles : ResourceDictionary
{
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(BaseShellItem))]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, "Microsoft.Maui.Controls.MenuShellItem", "Microsoft.Maui.Controls")]
	[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(RadioButton))]
	public iOSStyles() => InitializeComponent();
}
