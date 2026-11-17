using NUnit.Framework;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Auth;

/// <summary>
/// Base class of the test cases whose first precondition is "Not signed in". The session has no user and
/// no page is open: the test opens an address as a visitor and signs in with its own step
/// (<c>SignInPage.SignInAs</c>).
///
/// The sign-in page itself belongs to the identity provider and cannot be driven. What a test can check is
/// what the application does around it: it shows nothing of its own to a visitor, and it opens the page the
/// visitor asked for once the visitor is signed in.
///
/// The identity provider is a stand-in (<see cref="IdentityProviderStandIn"/>), installed by the base class, so no real tenant is needed.
/// </summary>
public abstract class VisitorTestBase : AuthenticatedTestBase
{
    protected override bool StartsSignedIn => false;

    protected override bool StartsInTheApplication => false;
}
