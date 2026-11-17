using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Organizations;

/// <summary>
/// The Organization screen (`/settings/organization`, Admins only): the logo or its placeholder, the name and
/// the profile and billing details, each with its label.
/// </summary>
public sealed class OrganizationPage : BasePage
{
    protected override string? PagePath => "/settings/organization";

    public OrganizationPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>"Click the "Edit" button." Returns when the Update Organization page is open.</summary>
    public async Task ClickEdit()
    {
        await Button("Edit").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/settings/organization/edit$"));
    }

    // ----- State -----

    /// <summary>The title "Billing Information".</summary>
    public ILocator Title => HeadingOf("Billing Information");

    /// <summary>The name of the organization under the logo.</summary>
    public ILocator OrganizationName(string name) =>
        Page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true, Level = 2 });

    /// <summary>
    /// The value shown for a label, for example the value of "VAT Number". It does not exist while the
    /// details are not shown.
    /// </summary>
    public ILocator Detail(string label) =>
        Page.Locator("dl > div")
            .Filter(new() { Has = Page.Locator("dt").GetByText(label, new() { Exact = true }) })
            .Locator("dd");

    /// <summary>The "Edit" button.</summary>
    public ILocator EditButton => Button("Edit");

    /// <summary>The logo image, which carries the name of the organization as its description.</summary>
    public ILocator Logo(string organizationName) =>
        Page.GetByRole(AriaRole.Img, new() { Name = organizationName, Exact = true });

    /// <summary>The first two letters of the name, shown in place of a logo.</summary>
    public ILocator LogoPlaceholder(string letters) => Page.GetByText(letters, new() { Exact = true });
}
