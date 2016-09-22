﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Crm.Sdk;
using InvoiceCalculation.CRM.Data;

namespace InvoiceCalculation.CRM.Model
{
    public class Engagement : EntityBase
    {
        public Engagement() : base(DataConstants.new_project) { }
        public Engagement(DynamicEntity e) : base(e) { }

        public Guid Id
        {
            get { return base.GetPropertyValue<Guid>("new_projectid", PropertyType.Key, Guid.Empty); }
            set { base.SetPropertyValue<Guid>("new_projectid", PropertyType.Key, value); }
        }

        public Guid ClientId
        {
            get { return base.GetPropertyValue<Guid>("new_accountid", PropertyType.Lookup, Guid.Empty); }
            set { base.SetPropertyValue<Guid>("new_accountid", PropertyType.Lookup, value); }
        }

        public String Name
        {
            get { return base.GetPropertyValue<String>("new_name", PropertyType.String, String.Empty); }
            set { base.SetPropertyValue<String>("new_name", PropertyType.String, value); }
        }

        public int? DaysToPay
        {
            get { return base.GetPropertyValue<int?>("new_daystopay", PropertyType.Number, null); }
            set { base.SetPropertyValue<int?>("new_daystopay", PropertyType.Number, value); }
        }

        public int ProductType
        {
            get { return base.GetPropertyValue<int>("new_producttype", PropertyType.Picklist, -1); }
            set { base.SetPropertyValue<int>("new_producttype", PropertyType.Picklist, value); }
        }

        public int Tier
        {
            get { return base.GetPropertyValue<int>("new_engagementtier", PropertyType.Picklist, -1); }
            set { base.SetPropertyValue<int>("new_engagementtier", PropertyType.Picklist, value); }
        }

        public DateTime EffectiveDate
        {
            get { return base.GetPropertyValue<DateTime>("new_effectiveengagementdate", PropertyType.DateTime, DateTime.MinValue); }
            set { base.SetPropertyValue<DateTime>("new_effectiveengagementdate", PropertyType.DateTime, value); }
        }

        public DateTime ExpirationDate
        {
            get { return base.GetPropertyValue<DateTime>("new_expirationdate", PropertyType.DateTime, DateTime.MinValue); }
            set { base.SetPropertyValue<DateTime>("new_expirationdate", PropertyType.DateTime, value); }
        }

        public DateTime ContractTerminationDate
        {
            get { return base.GetPropertyValue<DateTime>("new_contractterminationdate", PropertyType.DateTime, DateTime.MaxValue); }
            set { base.SetPropertyValue<DateTime>("new_contractterminationdate", PropertyType.DateTime, value); }
        }

        public DateTime EndDate
        {
            get { return base.GetPropertyValue<DateTime>("new_engagementenddate", PropertyType.DateTime, DateTime.MaxValue); }
            set { base.SetPropertyValue<DateTime>("new_engagementenddate", PropertyType.DateTime, value); }
        }

        public DateTime CurrentYear
        {
            get { return base.GetPropertyValue<DateTime>("new_estimatedstartdate", PropertyType.DateTime, DateTime.MinValue); }
            set { base.SetPropertyValue<DateTime>("new_estimatedstartdate", PropertyType.DateTime, value); }
        }

        public bool? IsProjectServices
        {
            get { return base.GetPropertyValue<bool?>("new_projectservices", PropertyType.Bit, null); }
            set { base.SetPropertyValue<bool?>("new_projectservices", PropertyType.Bit, value); }
        }

        public decimal FixedProjectFee
        {
            get { return base.GetPropertyValue<decimal>("new_fixedprojectfee", PropertyType.Money, 0m); }
            set { base.SetPropertyValue<decimal>("new_fixedprojectfee", PropertyType.Money, value); }
        }

        public bool IsWithinDateTime(DateTime dateTime)
        {
            var after = false;
            var before = false;

            if (this.EffectiveDate <= dateTime)
            {
                after = true;
            }

            if (this.EndDate >= dateTime)
            {
                before = true;
            }

            return after && before;
        }

        public int GetDaysToPay()
        {
            if (this.DaysToPay != null)
            {
                return (int)this.DaysToPay;
            }

            return 30;
        }

        /// <summary>
        /// Returns total asset value from plans with engagement instance.
        /// </summary>
        /// <param name="billingDate"></param>
        /// <param name="planEngagements"></param>
        /// <param name="plans"></param>
        /// <param name="planAssets"></param>
        /// <returns></returns>
        /// <remarks>
        /// Method is intended to comply with the following rules:
        ///     (1)  If erisa or vendor, don't set plan asset value.
        ///     (2)  If in advanced, grab latest asset value before invoice period start date.
        ///     (3)  If in arrears, grab latest asset value before/equal to invoice period end date.
        ///     (4)  Plan asset value as of date greater than or equal to beginning of billed on date quarter.
        /// </remarks>
        public decimal GetAssetsForInvoice(DateTime billingDate, List<PlanEngagement> planEngagements, List<PlanAccount> plans, List<PlanAsset> planAssets)
        {
            var result = 0m;

            var erisaVendorProductTypes = new int[] { 2, 5, 8, 11, 13, 14 };
            if (erisaVendorProductTypes.Contains(this.ProductType))
            {
                return result;
            }

            planEngagements = planEngagements.FindAll(x => x.EngagementId == this.Id);
            foreach (var planEngagement in planEngagements)
            {
                var plan = plans.Find(x => x.Id == planEngagement.PlanId);
                var assets = planAssets.FindAll(x => x.PlanId == plan.Id);
                assets = assets.FindAll(x => x.AssetValueAsOf <= billingDate.AddDays(1) && x.AssetValueAsOf >= billingDate.AddMonths(-3));
                assets = assets.OrderByDescending(x => x.AssetValueAsOf).ToList();

                if (assets.Count > 0)
                {
                    result = result + assets[0].AssetValue;
                }
            }

            return result;
        }

        public InvoiceCalculation.Model.ProductType GetProductTypeDetail()
        {
            return InvoiceCalculation.Data.ProductType.GetProductType(this.ProductType);
        }

        public bool IsNewOnBillingDate(DateTime billingDate)
        {
            var result = false;

            // if not quarterly, in advanced, ongoing; not relevant
            var productType = this.GetProductTypeDetail();

            if (!(productType.BillingFrequency == "Quarterly" && productType.BillingSchedule == "In Advance" && productType.BillingLength == "Ongoing"))
            {
                return false;
            }
            
            // only for ongoing engagements; start date and end date in same quarter
            var billingDateQuarter = (billingDate.Month - 1) / 3 + 1;
            var effectiveDateQuarter = (this.EffectiveDate.Month - 1) / 3 + 1;
            if (billingDateQuarter == effectiveDateQuarter && billingDate.Year == this.EffectiveDate.Year)
            {
                return true;
            }

            return result;
        }

        public bool IsTerminatedOnBillingDate(DateTime billingDate)
        {
            var result = false;

            // if not in advanced, not relevant
            var productType = this.GetProductTypeDetail();

            if (productType.BillingSchedule != "In Advance")
            {
                return false;
            }

            // only for ongoing engagements; start date and end date in same quarter
            var billingDateQuarter = (billingDate.Month - 1) / 3 + 1;
            var endDateQuarter = (this.ContractTerminationDate.Month - 1) / 3 + 1;
            if (billingDateQuarter == endDateQuarter && billingDate.Year == this.ContractTerminationDate.Year)
            {
                return true;
            }

            return result;
        }

        public DateTime GetInvoicePeriodStartDate(DateTime billingDate)
        {
            var productType = this.GetProductTypeDetail();
            if (productType.BillingFrequency == "Monthly")
            {
                var firstDayOfMonth = new DateTime(billingDate.Year, billingDate.Month, 1);
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                return firstDayOfMonth;
            }
            else if (productType.BillingFrequency == "Quarterly")
            {
                var billingDateQuarter = (billingDate.Month - 1) / 3 + 1;
                DateTime firstDayOfQuarter = new DateTime(billingDate.Year, (billingDateQuarter - 1) * 3 + 1, 1);
                DateTime lastDayOfQuarter = firstDayOfQuarter.AddMonths(3).AddDays(-1);
                return firstDayOfQuarter;
            }
            else // yearly
            {
                var firstDayOfYear = new DateTime(billingDate.Year, 1, 1);
                var lastDayOfYear = new DateTime(billingDate.Year, 12, 31);
                return firstDayOfYear;
            }
        }

        public DateTime GetInvoicePeriodEndDate(DateTime billingDate)
        {
            var productType = this.GetProductTypeDetail();
            if (productType.BillingFrequency == "Monthly")
            {
                var firstDayOfMonth = new DateTime(billingDate.Year, billingDate.Month, 1);
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                return lastDayOfMonth;
            }
            else if (productType.BillingFrequency == "Quarterly")
            {
                var billingDateQuarter = (billingDate.Month - 1) / 3 + 1;
                DateTime firstDayOfQuarter = new DateTime(billingDate.Year, (billingDateQuarter - 1) * 3 + 1, 1);
                DateTime lastDayOfQuarter = firstDayOfQuarter.AddMonths(3).AddDays(-1);
                return lastDayOfQuarter;
            }
            else // yearly
            {
                var firstDayOfYear = new DateTime(billingDate.Year, 1, 1);
                var lastDayOfYear = new DateTime(billingDate.Year, 12, 31);
                return lastDayOfYear;
            }
        }
        
        public Guid GetGeneralLedgerAccountId()
        {
            var result = Guid.Empty;
            // logic
            return result;
        }

        public decimal FixedProjectFeeForInvoicePeriod(DateTime billingDate)
        {
            var result = 0m;

            if (this.IsProjectServices != true)
            {
                return result;
            }

            if (this._finalProjectTaskCompletedWithinInvoicePeriod(billingDate))
            {
                result = this.FixedProjectFee;
            }

            return result;
        }

        /// <summary>
        /// Returns bool representing if last project task was completed in the invoicing period for the engagement.
        /// </summary>
        /// <param name="billingDate"></param>
        /// <returns></returns>
        private bool _finalProjectTaskCompletedWithinInvoicePeriod(DateTime billingDate)
        {
            var startDate = this.GetInvoicePeriodStartDate(billingDate);
            var endDate = this.GetInvoicePeriodEndDate(billingDate);
            
            var projectTaskCodes = new string[] { "E6", "I10", "RA2", "V2000" };

            var projectTasks = this.GetAllComponentTasks().FindAll(x => projectTaskCodes.Contains(x.TaskCode));
            var activeComponentTasks = projectTasks.FindAll(x => x.StateCode == 0);

            if (activeComponentTasks.Count > 0)
            {
                return false;
            }
            
            var result = true;
            foreach (var componentTask in projectTasks.FindAll(x => x.StateCode == 1))
            {
                var completedOn = componentTask.CompletedOn();
                if (completedOn == null)
                {
                    result = false;
                }

                var after = false;
                var before = false;

                if (completedOn <= endDate)
                {
                    before = true;
                }

                if (completedOn >= startDate)
                {
                    after = true;
                }

                if (!(before && after))
                {
                    result = false;
                }
            }

            return result;
        }

        public List<ComponentTask> GetAllComponentTasks()
        {
            return Data.ComponentTask.Retrieve(this);
        }
    }
}