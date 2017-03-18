﻿// -----------------------------------------------------------------------
// <copyright file="CustomerAccountController.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Store.PartnerCenter.CustomerPortal.Controllers
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Http;
    using BusinessLogic;
    using BusinessLogic.Exceptions;
    using Filters;
    using Filters.WebApi;
    using Models;
    using Newtonsoft.Json;
    using PartnerCenter.Models;
    using PartnerCenter.Models.Customers;
    using PartnerCenter.Models.Invoices;
    using PartnerCenter.Models.Subscriptions;
    using RequestContext;

    /// <summary>
    /// Customer Account API Controller.
    /// </summary>
    [RoutePrefix("api/CustomerAccounts")]
    public class CustomerAccountController : BaseController 
    {
        /// <summary>
        /// Gets the logged in customer account details. 
        /// </summary>
        /// <returns>The Customer account details from PC Customer Profile.</returns>
        [Route("")]
        [@Authorize(UserRole = UserRole.Customer)]
        [HttpGet]
        public async Task<CustomerViewModel> GetCustomerAccount()
        {
            string clientCustomerId = this.Principal.PartnerCenterCustomerId;

            // responseCulture determines decimals, currency and such
            CultureInfo responseCulture = new CultureInfo(ApplicationDomain.Instance.PortalLocalization.Locale);

            // localeSpecificApiClient allows pulling offer names localized to supported portal locales compatible with Offer API supported locales. 
            var localeSpecificPartnerCenterClient = ApplicationDomain.Instance.PartnerCenterClient.With(RequestContextFactory.Instance.Create(ApplicationDomain.Instance.PortalLocalization.OfferLocale));
            var customerAllSubscriptions = await localeSpecificPartnerCenterClient.Customers.ById(clientCustomerId).Subscriptions.GetAsync();

            List<CustomerLicensesModel> allSubscriptionsOfCustomer = (from item in customerAllSubscriptions.Items
                where item.BillingType == BillingType.License
                select new CustomerLicensesModel()
                {
                    Id = item.Id,
                    OfferName = item.OfferName,
                    Quantity = item.Quantity.ToString("G", responseCulture),
                    Status = this.GetStatusType(item.Status),
                    CreationDate = item.CreationDate.ToString("d", responseCulture)
                }).ToList();

            List<CustomerUsageSubscriptionsModel> allUsageSubscriptionsOfCustomer = (from item in customerAllSubscriptions.Items
                where item.BillingType == BillingType.Usage
                select new CustomerUsageSubscriptionsModel()
                {                                                                          
                    Id = item.Id,
                    Name = item.FriendlyName,                             
                    Status = this.GetStatusType(item.Status),
                    CreationDate = item.CreationDate.ToString("d", responseCulture)
                }).ToList();

            ////Loop through each usage subscription and find its cost * markup
            foreach (CustomerUsageSubscriptionsModel usageSubscription in allUsageSubscriptionsOfCustomer)
            {
                string selectedSubscriptionId = usageSubscription.Id as string;
                var usageRecords = localeSpecificPartnerCenterClient.Customers.ById(clientCustomerId).Subscriptions.ById(selectedSubscriptionId).UsageRecords.Resources.Get();
                decimal usage = new decimal(0);
                foreach (var record in usageRecords.Items)
                {
                    usage += record.TotalCost;
                }
                
                decimal markup = new decimal(1.20);
                usageSubscription.Usage = "$" + System.Math.Round(usage * markup, 2).ToString();
                var usageSummary = localeSpecificPartnerCenterClient.Customers.ById(clientCustomerId).Subscriptions.ById(selectedSubscriptionId).UsageSummary.Get();
                usageSubscription.BillingStartDate = usageSummary.BillingStartDate.ToString("MM/dd/yyyy");
            }

            return new CustomerViewModel()
            {
                Licenses = allSubscriptionsOfCustomer.OrderBy(items => items.OfferName),
                UsageSubscriptions = allUsageSubscriptionsOfCustomer.OrderBy(items => items.Name)                
            };
        }

        /// <summary>
        /// Registers a new customer and returns customer registration data.
        /// </summary>
        /// <param name="customerViewModel">The customer's registration information.</param>
        /// <returns>A registered customer object.</returns>
        [Route("")]
        [HttpPost]
        [@Authorize(UserRole = UserRole.None)]
        public async Task<CustomerViewModel> Register([FromBody] CustomerViewModel customerViewModel)
        {
            if (!ModelState.IsValid)
            {
                var errorList = (from item in ModelState.Values
                                 from error in item.Errors
                                 select error.ErrorMessage).ToList();                
                string errorMessage = JsonConvert.SerializeObject(errorList);
                throw new PartnerDomainException(ErrorCode.InvalidInput).AddDetail("ErrorMessage", errorMessage);
            }

            Customer newCustomer = null;
            
            // TODO :: Loc. may need special handling for national clouds deployments (China).
            string domainName = string.Format(CultureInfo.InvariantCulture, "{0}.onmicrosoft.com", customerViewModel.DomainPrefix);  

            // check domain available.
            bool isDomainTaken = await ApplicationDomain.Instance.PartnerCenterClient.Domains.ByDomain(domainName).ExistsAsync();
            if (isDomainTaken)
            {                                    
                throw new PartnerDomainException(ErrorCode.DomainNotAvailable).AddDetail("DomainPrefix", domainName);
            }

            // get the locale, we default to the first locale used in a country for now.
            var customerCountryValidationRules = await ApplicationDomain.Instance.PartnerCenterClient.CountryValidationRules.ByCountry(customerViewModel.Country).GetAsync();
            string billingCulture = customerCountryValidationRules.SupportedCulturesList.FirstOrDefault();      // default billing culture is the first supported culture for the customer's selected country. 
            string billingLanguage = customerCountryValidationRules.SupportedLanguagesList.FirstOrDefault();    // default billing culture is the first supported language for the customer's selected country. 

            newCustomer = new Customer()
            {
                CompanyProfile = new CustomerCompanyProfile()
                {
                    Domain = domainName,
                },
                BillingProfile = new CustomerBillingProfile()
                {
                    Culture = billingCulture,
                    Language = billingLanguage,
                    Email = customerViewModel.Email,
                    CompanyName = customerViewModel.CompanyName,

                    DefaultAddress = new Address()
                    {
                        FirstName = customerViewModel.FirstName,
                        LastName = customerViewModel.LastName,
                        AddressLine1 = customerViewModel.AddressLine1,
                        AddressLine2 = customerViewModel.AddressLine2,
                        City = customerViewModel.City,
                        State = customerViewModel.State,
                        Country = customerViewModel.Country,
                        PostalCode = customerViewModel.ZipCode,
                        PhoneNumber = customerViewModel.Phone,
                    }
                }
            };

            newCustomer = await ApplicationDomain.Instance.PartnerCenterClient.Customers.CreateAsync(newCustomer);
            
            return new CustomerViewModel()
            {
                AddressLine1 = newCustomer.BillingProfile.DefaultAddress.AddressLine1,
                AddressLine2 = newCustomer.BillingProfile.DefaultAddress.AddressLine2,
                City = newCustomer.BillingProfile.DefaultAddress.City,
                State = newCustomer.BillingProfile.DefaultAddress.State,
                ZipCode = newCustomer.BillingProfile.DefaultAddress.PostalCode,
                Country = newCustomer.BillingProfile.DefaultAddress.Country,
                Phone = newCustomer.BillingProfile.DefaultAddress.PhoneNumber,
                Language = newCustomer.BillingProfile.Language,
                FirstName = newCustomer.BillingProfile.DefaultAddress.FirstName,
                LastName = newCustomer.BillingProfile.DefaultAddress.LastName,
                Email = newCustomer.BillingProfile.Email,
                CompanyName = newCustomer.BillingProfile.CompanyName,
                MicrosoftId = newCustomer.CompanyProfile.TenantId,
                UserName = customerViewModel.Email,                
                Password = newCustomer.UserCredentials.Password,
                AdminUserAccount = newCustomer.UserCredentials.UserName + "@" + newCustomer.CompanyProfile.Domain
            };
        }

        /// <summary>
        /// Retrieves the localized status type string. 
        /// </summary>
        /// <param name="statusType">The subscription status type.</param>
        /// <returns>Localized Operation Type string.</returns>
        private string GetStatusType(SubscriptionStatus statusType)
        {
            switch (statusType)
            {
                case SubscriptionStatus.Active:
                    return Resources.SubscriptionStatusTypeActive;
                case SubscriptionStatus.Deleted:
                    return Resources.SubscriptionStatusTypeDeleted;
                case SubscriptionStatus.None:
                    return Resources.SubscriptionStatusTypeNone;
                case SubscriptionStatus.Suspended:
                    return Resources.SubscriptionStatusTypeSuspended;
                default:
                    return string.Empty;
            }
        }
    }
}