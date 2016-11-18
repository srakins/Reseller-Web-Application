// -----------------------------------------------------------------------
// <copyright file="PartnerCenterCustomersRepository.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Store.PartnerCenter.CustomerPortal.BusinessLogic.Customers
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Exceptions;
    using WindowsAzure.Storage.Table;

    /// <summary>
    /// Maintains Partner Center customers registered with the partner. Callers can associate AD tenants with Partner Center customers
    /// and retrieve that association.
    /// </summary>
    public class PartnerCenterCustomersRepository : DomainObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartnerCenterCustomersRepository"/> class.
        /// </summary>
        /// <param name="applicationDomain">An application domain instance.</param>
        public PartnerCenterCustomersRepository(ApplicationDomain applicationDomain) : base(applicationDomain)
        {
        }

        /// <summary>
        /// Registers a new Partner Center customer and ties it to the given Active Directory tenant.
        /// </summary>
        /// <param name="tenantId">The AD tenant ID which will be mapped to the given Partner Center customer.</param>
        /// <param name="partnerCenterCustomerId">The Partner Center customer ID.</param>
        /// <returns>A task.</returns>
        public async Task RegisterAsync(string tenantId, string partnerCenterCustomerId)
        {
            tenantId.AssertNotEmpty("tenantId can't be empty");
            partnerCenterCustomerId.AssertNotEmpty("partnerCenterCustomerId can't be empty");
            
            // ensure there is no existing association for the given tenant
            var existingCustomerId = await this.RetrieveAsync(tenantId);

            if (!string.IsNullOrWhiteSpace(existingCustomerId))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,  
                    Resources.ADTenantIsAlreadyRegisteredToCustomer, 
                    tenantId,
                    existingCustomerId));
            }

            // add the association
            var customersTable = await this.ApplicationDomain.AzureStorageService.GetPartnerCenterCustomersTableAsync();
            TableOperation addNewCustomer = TableOperation.Insert(new TableEntity(tenantId, partnerCenterCustomerId), false);
            var addNewCustomerResult = await customersTable.ExecuteAsync(addNewCustomer);

            addNewCustomerResult.HttpStatusCode.AssertHttpResponseSuccess(
                ErrorCode.PersistenceFailure,
                "Could not register new Partner Center customer",
                addNewCustomerResult.Result);
        }

        /// <summary>
        /// Retrieves the registered Partner Center customer ID for the given Active Directory tenant.
        /// </summary>
        /// <param name="tenantId">The AD tenant ID to look up its Partner Center customer.</param>
        /// <returns>The Partner Center customer ID. Empty string is no association was found.</returns>
        public async Task<string> RetrieveAsync(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentException("tenantId can't be empty");
            }

            var customersTable = await this.ApplicationDomain.AzureStorageService.GetPartnerCenterCustomersTableAsync();
            var customerQuery = new TableQuery<TableEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, tenantId));
            var resultingCustomers = await customersTable.ExecuteQuerySegmentedAsync(customerQuery, null);

            return resultingCustomers.FirstOrDefault()?.RowKey;
        }
    }
}