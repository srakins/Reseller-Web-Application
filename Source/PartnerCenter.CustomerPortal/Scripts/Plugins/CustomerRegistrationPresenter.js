/// <reference path="~/Scripts/_references.js" />

Microsoft.WebPortal.CustomerRegistrationPresenter = function (webPortal, feature, context) {
    /// <summary>
    /// Manages the offers experience. 
    /// </summary>
    /// <param name="webPortal">The web portal instance.</param>
    /// <param name="feature">The feature for which this presenter is created.</param>
    this.base.constructor.call(this, webPortal, feature, "Home", "/Template/CustomerRegistration/");

    this.addSubscriptionsView = new Microsoft.WebPortal.Views.AddSubscriptionsView(webPortal, "#AddSubscriptionsViewContainer", context);
    this.customerProfileView = new Microsoft.WebPortal.Views.NewCustomerProfileView(webPortal, "#CustomerProfileContainer");    

    this.context = context;
    this.customerRegistrationInfo;
    this.isPosting = false;

    var self = this;    

    this.onFormSubmit = function () {
        if (self.isPosting) {
            return;
        }

        if ($("#Form").valid()) {
            if (self.addSubscriptionsView.subscriptionsList.rows().length <= 0) {
                self.webPortal.Services.Dialog.show("emptyOffersErrorMessage-template", {}, [
                    Microsoft.WebPortal.Services.Button.create(Microsoft.WebPortal.Services.Button.StandardButtons.OK, self.webPortal.Resources.Strings.OK, function () {
                        self.webPortal.Services.Dialog.hide();
                    })
                ]);

                return;
            }

            self.isPosting = true;
            var customerNotification;

            // if client id is already present then skip the create Customer call. 
            // Call Create Customer if customer is not registered (or is retring due an error from the past).  
            var customerId = this.customerProfileView.viewModel.CustomerMicrosoftID();            
            if (!customerId) {
                customerNotification = new Microsoft.WebPortal.Services.Notification(Microsoft.WebPortal.Services.Notification.NotificationType.Progress, self.webPortal.Resources.Strings.Plugins.CustomerRegistrationPage.CustomerRegistrationMessage);
                self.webPortal.Services.Notifications.add(customerNotification);

                new Microsoft.WebPortal.Utilities.RetryableServerCall(self.webPortal.Helpers.ajaxCall("api/CustomerAccounts",
                        Microsoft.WebPortal.HttpMethod.Post,
                        self.getCustomerInformation(),
                        Microsoft.WebPortal.ContentType.Json, 120000),
                    "RegisterCustomer", []).execute()
                    // Success of Create Customer API Call. 
                    .done(function (registrationConfirmation) { 
                        // Reset the CustomerMicrosoftID to CustomerId returned. 
                        self.customerProfileView.viewModel.CustomerMicrosoftID(registrationConfirmation.MicrosoftId);
                        self.customerRegistrationInfo = registrationConfirmation; // maintain this for future retries while user is still on the same page. 

                        // turn the notification into a success
                        customerNotification.type(Microsoft.WebPortal.Services.Notification.NotificationType.Success);
                        notificationMessage = self.webPortal.Resources.Strings.Plugins.CustomerRegistrationPage.CustomerRegistrationSuccessMessage + " - " + registrationConfirmation.CompanyName + " (" + registrationConfirmation.MicrosoftId + ")";
                        customerNotification.message(notificationMessage);
                        customerNotification.buttons([
                            Microsoft.WebPortal.Services.Button.create(Microsoft.WebPortal.Services.Button.StandardButtons.OK, self.webPortal.Resources.Strings.OK, function () {
                                customerNotification.dismiss();
                            })
                        ]);

                        // raise the Order passing along the registrationConfirmation data object.  
                        // self.raiseOrder(customerNotification, self.customerRegistrationInfo);                        
                        var registeredCustomer = registrationConfirmation;
                        var registrationConfirmationInfo = {                            
                            SubscriptionsToOrder: self.getSubscriptions(),
                            AddressLine1: registeredCustomer.AddressLine1,
                            AddressLine2: registeredCustomer.AddressLine2,
                            AdminUserAccount: registeredCustomer.AdminUserAccount,
                            Password: registeredCustomer.Password,
                            City: registeredCustomer.City,
                            CompanyName: registeredCustomer.CompanyName,
                            Country: registeredCustomer.Country,
                            Email: registeredCustomer.Email,
                            FirstName: registeredCustomer.FirstName,
                            Language: registeredCustomer.Language,
                            LastName: registeredCustomer.LastName,
                            MicrosoftId: registeredCustomer.MicrosoftId,
                            Phone: registeredCustomer.Phone,
                            State: registeredCustomer.State,
                            UserName: registeredCustomer.UserName,
                            ZipCode: registeredCustomer.ZipCode
                        }

                        // hand it off to the registration summary presenter
                        self.webPortal.Journey.advance(Microsoft.WebPortal.Feature.RegistrationConfirmation, registrationConfirmationInfo);
                    })
                    // Failure of Create Customer API Call. 
                    .fail(function (result, status, error) {                        
                        self.customerProfileView.viewModel.CustomerMicrosoftID(""); // we want this clear so that create customer call can be retried by user. 

                        customerNotification.type(Microsoft.WebPortal.Services.Notification.NotificationType.Error);
                        customerNotification.buttons([
                            Microsoft.WebPortal.Services.Button.create(Microsoft.WebPortal.Services.Button.StandardButtons.OK, self.webPortal.Resources.Strings.OK, function () {
                                customerNotification.dismiss();
                            })
                        ]);

                        var errorPayload = JSON.parse(result.responseText);

                        if (errorPayload) {
                            switch (errorPayload.ErrorCode) {
                                case Microsoft.WebPortal.ErrorCode.InvalidAddress:
                                    customerNotification.message(self.webPortal.Resources.Strings.Plugins.CustomerRegistrationPage.InvalidAddress);
                                    break;
                                case Microsoft.WebPortal.ErrorCode.DomainNotAvailable:
                                    customerNotification.message(self.webPortal.Resources.Strings.Plugins.CustomerRegistrationPage.DomainNotAvailable);
                                    break;
                                case Microsoft.WebPortal.ErrorCode.InvalidInput:
                                    customerNotification.message(self.webPortal.Resources.Strings.Plugins.CustomerRegistrationPage.InvalidInputErrorPrefix + errorPayload.Details.ErrorMessage);
                                    break;
                                case Microsoft.WebPortal.ErrorCode.DownstreamServiceError:
                                    customerNotification.message(self.webPortal.Resources.Strings.Plugins.CustomerRegistrationPage.DownstreamErrorPrefix + errorPayload.Details.ErrorMessage);
                                    break;
                                default:
                                    customerNotification.message(self.webPortal.Resources.Strings.Plugins.CustomerRegistrationPage.CustomerRegistrationFailureMessage);
                                    break;
                            }
                        } else {
                            customerNotification.message(self.webPortal.Resources.Strings.Plugins.CustomerRegistrationPage.CustomerRegistrationFailureMessage);
                        }
                    })     
                    .always(function () {
                        self.isPosting = false;
                    });
            }
        } else {
            // the form is invalid
        }
    }

    this.getSubscriptions = function () {
        var orders = [];

        for (var i in this.addSubscriptionsView.subscriptionsList.rows()) {
            orders.push({
                OfferId: this.addSubscriptionsView.subscriptionsList.rows()[i].offer.Id,
                SubscriptionId: this.addSubscriptionsView.subscriptionsList.rows()[i].offer.Id,
                Quantity: this.addSubscriptionsView.subscriptionsList.rows()[i].quantity()
            });
        }

        return orders;
    }

    this.getCustomerInformation = function () {
        var customerInformation = {            
            Country: this.customerProfileView.viewModel.Country(),
            CompanyName: this.customerProfileView.viewModel.CompanyName(),
            AddressLine1: this.customerProfileView.viewModel.AddressLine1(),            
            AddressLine2: this.customerProfileView.viewModel.AddressLine2(),
            City: this.customerProfileView.viewModel.City(),            
            State: this.customerProfileView.viewModel.State(),
            ZipCode: this.customerProfileView.viewModel.ZipCode(),            
            Email: this.customerProfileView.viewModel.Email(),
            Password: this.customerProfileView.viewModel.Password(),
            PasswordConfirmation: this.customerProfileView.viewModel.PasswordConfirmation(),
            FirstName: this.customerProfileView.viewModel.FirstName(),
            LastName: this.customerProfileView.viewModel.LastName(),
            Phone: this.customerProfileView.viewModel.Phone(),            
            DomainPrefix: this.customerProfileView.viewModel.DomainPrefix()
        }

        return customerInformation;
    }
}

// inherit BasePresenter
$WebPortal.Helpers.inherit(Microsoft.WebPortal.CustomerRegistrationPresenter, Microsoft.WebPortal.Core.TemplatePresenter);

Microsoft.WebPortal.CustomerRegistrationPresenter.prototype.onActivate = function () {
    /// <summary>
    /// Called when the presenter is activated.
    /// </summary>
}

Microsoft.WebPortal.CustomerRegistrationPresenter.prototype.onRender = function () {
    /// <summary>
    /// Called when the presenter is about to be rendered.
    /// </summary>

    ko.applyBindings(this, $("#Form")[0]);

    this.addSubscriptionsView.render();
    this.customerProfileView.render();
}

Microsoft.WebPortal.CustomerRegistrationPresenter.prototype.onShow = function () {
    /// <summary>
    /// Called when content is shown.
    /// </summary>

    this.addSubscriptionsView.show();
    this.customerProfileView.show();    
}

//@ sourceURL=CustomerRegistrationPresenter.js