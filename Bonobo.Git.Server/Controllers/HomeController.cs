﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;

using Bonobo.Git.Server.App_GlobalResources;
using Bonobo.Git.Server.Configuration;
using Bonobo.Git.Server.Data;
using Bonobo.Git.Server.Helpers;
using Bonobo.Git.Server.Models;
using Bonobo.Git.Server.Security;

using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Practices.Unity;
using Bonobo.Git.Server.Owin.Windows;

namespace Bonobo.Git.Server.Controllers
{
    public class HomeController : Controller
    {
        [Dependency]
        public IMembershipService MembershipService { get; set; }

        [Dependency]
        public IAuthenticationProvider AuthenticationProvider { get; set; }

        [WebAuthorize]
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Repository");
        }

        public ActionResult PageNotFound()
        {
            return View();
        }

        public ActionResult ServerError()
        {
            return View();
        }

        public ActionResult Error()
        {
            return View();
        }

        public ActionResult ResetPassword(string digest)
        {
            digest = HttpUtility.UrlDecode(digest, Encoding.UTF8);
            var cacheObj = MvcApplication.Cache[digest];
            if (cacheObj != null)
            {
                var username = cacheObj.ToString();
                return View(new ResetPasswordModel {Username = username});
            }
            else
            {
                ModelState.AddModelError("", "Password reset link was not valid");
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public ActionResult ResetPassword(ResetPasswordModel model)
        {
            if (ModelState.IsValid)
            {
                var user = MembershipService.GetUserModel(model.Username);
                if (user == null)
                {
                    TempData["ResetSuccess"] = false;
                    Response.AppendToLog("FAILURE");
                }
                else
                {
                    MembershipService.UpdateUser(user.Id, null, null, null, null, model.Password);
                    TempData["ResetSuccess"] = true;
                }
            }
            return View(model);
        }

        public ActionResult ForgotPassword()
        {
            return View(new ForgotPasswordModel());
        }

        [HttpPost]
        public ActionResult ForgotPassword(ForgotPasswordModel model)
        {
            if (ModelState.IsValid)
            {
                var user = MembershipService.GetUserModel(model.Username);
                if (user == null)
                {
                    ModelState.AddModelError("", Resources.Home_ForgotPassword_UserNameFailure);
                    Response.AppendToLog("FAILURE");
                }
                else
                {
                    string token = MembershipService.GenerateResetToken(user.Username);
                    MvcApplication.Cache.Add(token, model.Username, DateTimeOffset.Now.AddHours(1));

                    // Passing Requust.Url.Scheme to Url.Action forces it to generate a full URL
                    var resetUrl = Url.Action("ResetPassword", "Home", new {digest = HttpUtility.UrlEncode(Encoding.UTF8.GetBytes(token))},Request.Url.Scheme);

                    TempData["SendSuccess"] = MembershipHelper.SendForgotPasswordEmail(user, resetUrl);
                }
            }
            return View(model);
        }

        [AllowAnonymous]
        [HttpGet]
        public ActionResult WindowsLogin(string returnUrl)
        {
            if (String.IsNullOrEmpty(User.Identity.Name))
            {
                AuthenticationProperties authenticationProperties = new AuthenticationProperties()
                {
                    RedirectUri = returnUrl
                };

                Request.GetOwinContext().Authentication.Challenge(authenticationProperties, WindowsAuthenticationDefaults.AuthenticationType);
                return new EmptyResult();
            }

            return Redirect(returnUrl);
        }

        public ActionResult LogOn(string returnUrl)
        {
            return View(new LogOnModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        public ActionResult LogOn(LogOnModel model)
        {
            if (ModelState.IsValid)
            {
                ValidationResult result = MembershipService.ValidateUser(model.Username, model.Password);
                switch (result)
                {
                    case ValidationResult.Success:
                        AuthenticationProvider.SignIn(model.Username, Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : Url.Action("Index", "Home"));
                        Response.AppendToLog("SUCCESS");
                        return new EmptyResult();
                    case ValidationResult.NotAuthorized:
                        return new RedirectResult("~/Home/Unauthorized");
                    default:
                        ModelState.AddModelError("", Resources.Home_LogOn_UsernamePasswordIncorrect);
                        Response.AppendToLog("FAILURE");
                        break;
                }                
            }

            return View(model);
        }

        public ActionResult LogOff()
        {
            AuthenticationProvider.SignOut();
            return RedirectToAction("Index", "Home");
        }

        public ActionResult Unauthorized()
        {
            return View();
        }

        public ActionResult ChangeCulture(string lang, string returnUrl)
        {
            Session["Culture"] = new CultureInfo(lang);
            return Redirect(returnUrl);
        }

        public ActionResult Diagnostics()
        {
            if (Request.IsLocal)
            {
                var verifier = new DiagnosticReporter();
                return Content(verifier.GetVerificationReport(), "text/plain", Encoding.UTF8);
            }
            else
            {
                return Content("You can only run the diagnostics locally to the server");
            }
        }
    }
}
