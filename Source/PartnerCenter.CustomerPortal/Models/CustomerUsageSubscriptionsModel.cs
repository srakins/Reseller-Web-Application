﻿// -----------------------------------------------------------------------
// <copyright file="CustomerUsageSubscriptionsModel.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Store.PartnerCenter.CustomerPortal.Models
{
    /// <summary>
    /// The customer licenses view model.
    /// </summary>
    public class CustomerUsageSubscriptionsModel
    {
        /// <summary>
        /// Gets or sets the customer license Id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the friendly name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the usage for this Subscription
        /// </summary>
        public string Usage { get; set; }

        /// <summary>
        /// Gets or sets the customer license status like None, Active, Suspended or Deleted
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the license creation date. 
        /// </summary>
        public string CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the billing start date. 
        /// </summary>
        public string BillingStartDate { get; set; }
    }
}