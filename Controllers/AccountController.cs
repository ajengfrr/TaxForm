using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using TaxForm.Models;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web.UI.Areas.MicrosoftIdentity.Controllers;
//using Microsoft.Graph;

namespace TaxForm.Controllers
{
    public class AccountController : Controller
    {
        //private GraphServiceClient _graphServiceClient;

        //public AccountController(GraphServiceClient graphServiceClient)
        //{
        //    _graphServiceClient = graphServiceClient;
        //}

        public IActionResult Index()
        {
            return View();
        }

        public List<UserModel> users = null;
        //public AccountController()
        //{
        //    users = new List<UserModel>();
        //    users.Add(new UserModel()
        //    {
        //        UserId = 1,
        //        Username = "stepanus.triatmaja",
        //        Password = "Password1!",
        //        Role = "Admin"
        //    });
        //    users.Add(new UserModel()
        //    {
        //        UserId = 2,
        //        Username = "user1",
        //        Password = "Password1!",
        //        Role = "User"
        //    });
        //}
        public IActionResult Login(string ReturnUrl = "/")
        {
            LoginModel objLoginModel = new LoginModel();
            objLoginModel.ReturnUrl = ReturnUrl;
            return View(objLoginModel);
        }
        [HttpPost]
        public async Task<IActionResult> Login(LoginModel objLoginModel)
        {
            if (ModelState.IsValid)
            {
                var user = users.Where(x => x.Username == objLoginModel.UserName 
                && x.Password == objLoginModel.Password).FirstOrDefault();
                if (user == null)
                {
                    ViewBag.Message = "Invalid Credential";
                    return View(objLoginModel);
                }
                else
                {
                    var claims = new List<Claim>() {
                    new Claim(ClaimTypes.NameIdentifier, Convert.ToString(user.UserId)),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.Role),
                        new Claim("FavoriteDrink", "Tea")
                    };
                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties()
                    {
                        IsPersistent = objLoginModel.RememberLogin
                    });
                    return LocalRedirect(objLoginModel.ReturnUrl);
                }
            }
            return View(objLoginModel);
        }
        //public async Task<IActionResult> LogOut()
        //{
        //    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        //    return LocalRedirect("/");
        //}

        [HttpGet]
        public IActionResult LogOut()
        {
            var callbackUrl = Url.Action(nameof(SignedOut), "Account", values: null, protocol: Request.Scheme);
            return SignOut(
                new AuthenticationProperties { RedirectUri = callbackUrl },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }


        [HttpGet]
        public IActionResult SignedOut()
        {
            if (User.Identity.IsAuthenticated)
            {
                // Redirect to home page if the user is authenticated.
                return Redirect("/MicrosoftIdentity/Account/SignIn");
                //return RedirectToAction(nameof(AccountController.Index), "/");
            }
            return Redirect("/MicrosoftIdentity/Account/SignIn");
            //return RedirectToAction(nameof(TrTaxesController.Index), "pathtoberedirectedto");
        }
    }
}
