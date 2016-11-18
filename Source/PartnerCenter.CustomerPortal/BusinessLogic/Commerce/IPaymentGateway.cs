// -----------------------------------------------------------------------
// <copyright file="IPaymentGateway.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Store.PartnerCenter.CustomerPortal.BusinessLogic.Commerce
{
    using System.Threading.Tasks;

    /// <summary>
    /// The payment gateway contract. Implement this interface to provide payment capabilities.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>
        /// Executes a payment. 
        /// </summary>
        /// <returns>An Authorization code.</returns>
        Task<string> ExecutePaymentAsync();

        /// <summary>
        /// Finalizes an authorized payment.
        /// </summary>
        /// <param name="authorizationCode">The authorization code for the payment to capture.</param>
        /// <returns>A task.</returns>
        Task CaptureAsync(string authorizationCode);

        /// <summary>
        /// Voids an authorized payment.
        /// </summary>
        /// <param name="authorizationCode">The authorization code for the payment to void.</param>
        /// <returns>a Task</returns>
        Task VoidAsync(string authorizationCode);
    }
}