namespace Sitecore.Support.Modules.EmailCampaign.Statistics.DataSources
{
    using Sitecore;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.EmailCampaign.Analytics.Model;
    using Sitecore.Modules.EmailCampaign.Core;
    using Sitecore.Modules.EmailCampaign.Core.Analytics;
    using Sitecore.Modules.EmailCampaign.Exceptions;
    using Sitecore.Modules.EmailCampaign.Factories;
    using Sitecore.Modules.EmailCampaign.Recipients;
    using Sitecore.Modules.EmailCampaign.Statistics;
    using Sitecore.Modules.EmailCampaign.Statistics.DataSources.Interfaces;
    using Sitecore.Modules.EmailCampaign.Xdb;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;

    public class ContactsStatesDataSource : IContactsStatesDataSource
    {
        private readonly CoreFactory coreFactory;
        private readonly EcmFactory ecmFactory;
        private readonly RecipientRepository recipientRepository;
        private readonly ReportDataProviderExt reportDataProvider;

        public ContactsStatesDataSource(EcmFactory ecmFactory, CoreFactory coreFactory, ReportDataProviderExt reportDataProvider, RecipientRepository recipientRepository)
        {
            Assert.ArgumentNotNull(ecmFactory, "ecmFactory");
            Assert.ArgumentNotNull(coreFactory, "coreFactory");
            Assert.ArgumentNotNull(reportDataProvider, "reportDataProvider");
            Assert.ArgumentNotNull(recipientRepository, "recipientRepository");
            this.ecmFactory = ecmFactory;
            this.coreFactory = coreFactory;
            this.reportDataProvider = reportDataProvider;
            this.recipientRepository = recipientRepository;
        }

        public DataTable GetContactsStatesData(Guid planId, string[] automationStates, string language, DataPage page)
        {
            Assert.ArgumentNotNull(automationStates, "automationStates");
            DataTable emptyDataTable = this.GetEmptyDataTable();
            if (planId != Guid.Empty)
            {
                Item itemFromContentDb = this.coreFactory.GetItemUtilExt().GetItemFromContentDb(new ID(planId));
                if (itemFromContentDb == null)
                {
                    throw new EmailCampaignException("No engagement plan item was found by the id '{0}'.", new object[] { planId });
                }
                string[] strArray = new string[automationStates.Length];
                Item[] source = new Item[automationStates.Length];
                for (int i = 0; i < automationStates.Length; i++)
                {
                    Item automationStateItem = this.coreFactory.GetItemUtilExt().GetAutomationStateItem(itemFromContentDb, automationStates[i]);
                    if (automationStateItem == null)
                    {
                        throw new EmailCampaignException("No automation state item '{0}' was found in the '{1}' engagement plan.", new object[] { automationStates[i], planId });
                    }
                    source[i] = automationStateItem;
                    Guid guid = automationStateItem.ID.ToGuid();
                    strArray[i] = $"new BinData(3, '{System.Convert.ToBase64String(guid.ToByteArray())}')";
                }
                string queryItemId = string.IsNullOrEmpty(language) ? ((page != null) ? "{13D44D6E-4376-488B-B357-BE9B1177F059}" : "{41095E03-E23B-424A-887F-7932E2BDBEC4}") : ((page != null) ? "{72301105-2A22-458F-9651-6586A028F7D9}" : "{48350F37-1ABA-4ACB-AD27-53D0EEE40F07}");
                Dictionary<string, object> queryParameters = new Dictionary<string, object> {
                    {
                        "@PlanId",
                        planId
                    }
                };
                if (!string.IsNullOrEmpty(language))
                {
                    queryParameters.Add("@Language", language);
                }
                if (page != null)
                {
                    queryParameters.Add("@Skip", page.Index * page.Size);
                    queryParameters.Add("@Limit", page.Size);
                }
                DataTable table2 = this.reportDataProvider.GetData(queryItemId, queryParameters, new object[] { string.Join(", ", strArray) });
                if (table2.Rows.Count == 0)
                {
                    return emptyDataTable;
                }
                IEnumerator enumerator = table2.Rows.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        Func<Item, bool> predicate = null;
                        DataRow row1 = (DataRow)enumerator.Current;
                        IAutomationStateContext automationState = this.ecmFactory.Gateways.AnalyticsGateway.GetAutomationState((Guid)row1["_id_ContactId"], planId);
                        if (automationState != null)
                        {
                            EcmCustomValues customData = (EcmCustomValues)automationState.GetCustomData("sc.ecm");
                            DataRow row = emptyDataTable.NewRow();
                            row["ContactId"] = row["_id_ContactId"];
                            row["Email"] = customData.Email;
                            row["Entry"] = automationState.EntryDateTime;
                            if (predicate == null)
                            {
                                predicate = a => a.ID.ToGuid() == ((Guid)row["StateId"]);
                            }
                            Item item = source.First<Item>(predicate);
                            row["StateName"] = this.coreFactory.GetItemUtilExt().GetItemFieldValue(item, FieldIDs.DisplayName);
                            Recipient recipient = this.recipientRepository.GetRecipient(new XdbContactId((Guid)row["_id_ContactId"]));
                            if (recipient != null)
                            {
                            if(recipient.GetProperties<Email>().DefaultProperty != null)
                                row["PreferredEmail"] = recipient.GetProperties<Email>().DefaultProperty.EmailAddress;
                            if(recipient.GetProperties<PersonalInfo>().DefaultProperty != null)
                                row["FullName"] = recipient.GetProperties<PersonalInfo>().DefaultProperty.FullName;
                            }
                            emptyDataTable.Rows.Add(row);
                        }
                    }
            }
            return emptyDataTable;
        }

        private DataTable GetEmptyDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("ContactId", typeof(string));
            table.Columns.Add("Email", typeof(string));
            table.Columns.Add("PreferredEmail", typeof(string));
            table.Columns.Add("Entry", typeof(DateTime));
            table.Columns.Add("FullName", typeof(string));
            table.Columns.Add("StateName", typeof(string));
            return table;
        }
    }
}
