using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Organizations;

/// <summary>
/// The Update Organization screen (`/settings/organization/edit`, Admins only): the name, the billing details
/// and the logo, with "Save Changes" and the "Unsaved Changes" badge.
/// </summary>
public sealed class UpdateOrganizationPage : BasePage
{
    protected override string? PagePath => "/settings/organization/edit";

    public UpdateOrganizationPage(IPage page) : base(page) { }

    /// <summary>"Open the Update Organization page." Returns when the form is shown or an error is.</summary>
    public override async Task Open()
    {
        await base.Open();
        await Assertions.Expect(Field("Organization Name").Or(Page.GetByRole(AriaRole.Alert)).First).ToBeVisibleAsync();
    }

    // ----- Actions -----

    /// <summary>"Clear the "City" field." The label is as shown, for example "Legal Name".</summary>
    public Task ClearField(string label) => Field(label).ClearAsync();

    /// <summary>"Type ... in the "City" field." Replaces what the field holds.</summary>
    public Task TypeInField(string label, string text) => TypeAsync(Field(label), text);

    /// <summary>
    /// "Click the "Save Changes" button." Returns when saving has finished, whatever the outcome (the button
    /// shows "Saving..." meanwhile).
    /// </summary>
    public async Task ClickSaveChanges()
    {
        await Button("Save Changes").ClickAsync();
        await Assertions.Expect(Button("Saving...")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// "Upload the file ... with the "Click to upload logo" button." Chooses the file in the dialog the button
    /// opens. Returns when the check of the file, or the upload, has finished, whatever the outcome.
    /// </summary>
    public async Task UploadLogo(string filePath)
    {
        LogStep("Upload logo", Path.GetFileName(filePath));
        var chooser = await Page.RunAndWaitForFileChooserAsync(() => LogoButton.ClickAsync());
        await chooser.SetFilesAsync(filePath);
        await Assertions.Expect(LogoButton).ToBeEnabledAsync();
    }

    /// <summary>
    /// Whether the page shows a logo (not the placeholder). Waits for the form to be loaded first, so that
    /// "the logo does not change" can be checked against what was there before.
    /// </summary>
    public async Task<bool> HasLogo()
    {
        await Assertions.Expect(Field("Organization Name")).ToBeVisibleAsync();
        return await Logo.CountAsync() > 0;
    }

    // ----- State -----

    /// <summary>The message under a field, for example "Organization name is required" under "Organization Name".</summary>
    public ILocator FieldMessage(string label) => Field(label).Locator("xpath=following-sibling::*[1]");

    /// <summary>A field by its label, for assertions on its value.</summary>
    public ILocator FormField(string label) => Field(label);

    /// <summary>The "Unsaved Changes" badge next to the title.</summary>
    public ILocator UnsavedChangesBadge => ExactText("Unsaved Changes");

    /// <summary>The title "Update Organization - {name}".</summary>
    public ILocator Title(string organizationName) =>
        HeadingWithTail("Update Organization", $" - {organizationName}");

    /// <summary>The upload button: the logo or its placeholder.</summary>
    public ILocator LogoButton => Button("Click to upload logo");

    /// <summary>The logo image inside the upload button; it does not exist while there is no logo.</summary>
    public ILocator Logo => LogoButton.Locator("img");

    /// <summary>The Update Organization page is open (the address ends with "/edit").</summary>
    public Task ExpectOpen() =>
        Assertions.Expect(Page).ToHaveURLAsync(new Regex("/settings/organization/edit$"));
}
