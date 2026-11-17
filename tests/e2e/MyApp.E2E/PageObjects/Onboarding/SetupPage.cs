using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Onboarding;

/// <summary>
/// The "Set Up Your Organization" screen (`/onboarding`): one step, "Organization Details", shown exactly
/// while the user belongs to no organization.
/// </summary>
public sealed class SetupPage : BasePage
{
    protected override string? PagePath => "/onboarding";

    public SetupPage(IPage page) : base(page) { }

    // ----- Actions: "Type ... in the "Organization Name" field." -----

    public Task TypeOrganizationName(string text) => TypeAsync(Field("Organization Name"), text);

    public Task TypeLegalName(string text) => TypeAsync(Field("Legal Name"), text);

    public Task TypeVatNumber(string text) => TypeAsync(Field("VAT Number"), text);

    public Task TypeBillingEmail(string text) => TypeAsync(Field("Billing Email"), text);

    /// <summary>"Click the "Submit" button." Returns when the request, if any, has finished, whatever the outcome.</summary>
    public async Task ClickSubmit()
    {
        await SubmitButton.ClickAsync();
        await WaitForPageLoadAsync();
        await Page.WaitForFunctionAsync(
            "() => !document.querySelector('#onboarding-wizard button[type=submit]:disabled')");
    }

    // ----- State -----

    /// <summary>The page title "Set Up Your Organization".</summary>
    public ILocator Title => HeadingOf("Set Up Your Organization");

    /// <summary>The step "Organization Details".</summary>
    public ILocator StepTitle => ExactText("Organization Details");

    /// <summary>The "Submit" button.</summary>
    public ILocator SubmitButton => Button("Submit");

    /// <summary>A "Skip for now" link or button; the page has none.</summary>
    public ILocator SkipLink => ExactText("Skip for now");

    /// <summary>The "Organization Name" field.</summary>
    public ILocator OrganizationNameField => Field("Organization Name");

    /// <summary>"Creating your organization..." while the organization is being created.</summary>
    public ILocator CreatingText => ExactText("Creating your organization...");

    /// <summary>The message of a field, such as "Organization name is required".</summary>
    public ILocator FieldMessage(string text) => ExactText(text);
}
