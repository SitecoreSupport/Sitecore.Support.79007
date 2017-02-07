using Sitecore.EmailCampaign.Server.Services;
using Sitecore.Diagnostics;
using Sitecore.EmailCampaign.Server.Contexts;
using Sitecore.EmailCampaign.Server.Filters;
using Sitecore.EmailCampaign.Server.Model;
using Sitecore.EmailCampaign.Server.Responses;
using Sitecore.EmailCampaign.Server.Services.Interfaces;
using Sitecore.Modules.EmailCampaign;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Statistics;
using Sitecore.Modules.EmailCampaign.Statistics.Repositories.Interfaces;
using Sitecore.Services.Core;
using Sitecore.Services.Infrastructure.Web.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web.Http;
using Sitecore.Analytics.Reporting;
using Sitecore.Modules.EmailCampaign.Exceptions;
using Sitecore.Modules.EmailCampaign.Factories;
using Sitecore.Modules.EmailCampaign.Messages;
using Sitecore.Modules.EmailCampaign.Recipients;
using Sitecore.Modules.EmailCampaign.Statistics.DataSources;
using Sitecore.Modules.EmailCampaign.Statistics.DataSources.Interfaces;

namespace Sitecore.Support.EmailCampaign.Server.Controllers.DataSource
{
    [ServicesController("EXM.MessageReport"), Sitecore.Support.EmailCampaign.Server.Filters.SitecoreAuthorize(new string[] { @"sitecore\ECM Advanced Users", @"sitecore\ECM Users" })]
    public class MessageReportController : ServicesApiController
    {
        private const string BestEmailLandingPagesReport = "{B25B894F-867D-4F49-A4D8-CFFFAB28C46D}";
        private readonly Dictionary<string, string> bestPagesMappings;
        private readonly IContactsStatesRepository contactsStatesRepository;
        private readonly IContactsStatesDataSource contactsStatesDataSource;
        private const string EmailBouncesDetailsReport = "{3C101F2E-2D9A-4A31-82A0-FAB3C979914B}";
        private const string EmailLandingPagesReport = "{FCA32D8F-7D95-41E7-9F25-B69D620D1680}";
        private readonly IEmailLandingPagesRepository emailLandingPagesRepository;
        private const string MessagePerformancePerLanguageReport = "{B8736652-76C6-428E-840D-67E363E593F8}";
        private readonly IMessageStatisticsService messageStatisticsService;
        private const string NoDataMarker = "-";
        private readonly SortParameterFactory sortParameterFactory;
        private const string UnsubscribedDetailsReport = "{5164EF31-7CF5-47A5-8B5B-2EDFB59FD93C}";

        public MessageReportController() : this(DependencyResolver.Resolve<IEmailLandingPagesRepository>(), DependencyResolver.Resolve<IContactsStatesRepository>(), new MessageStatisticsService(), new SortParameterFactory())
        {
        }

        public MessageReportController(IEmailLandingPagesRepository emailLandingPagesRepository, IContactsStatesRepository contactsStatesRepository, IMessageStatisticsService messageStatisticsService, SortParameterFactory sortParameterFactory)
        {
            Assert.ArgumentNotNull(emailLandingPagesRepository, "emailLandingPagesRepository");
            Assert.ArgumentNotNull(contactsStatesRepository, "contactsStatesRepository");
            Assert.ArgumentNotNull(sortParameterFactory, "sortParameterFactory");
            this.emailLandingPagesRepository = emailLandingPagesRepository;
            this.contactsStatesRepository = contactsStatesRepository;
            this.messageStatisticsService = messageStatisticsService;
            this.sortParameterFactory = sortParameterFactory;
            this.bestPagesMappings = new Dictionary<string, string>();
            this.bestPagesMappings.Add("ValuePerVisit", "Most Relevant");
            this.bestPagesMappings.Add("Value", "Most Valuable");
            this.bestPagesMappings.Add("Visits", "Most Clicked");
            this.bestPagesMappings.Add("Attention", "Most Attention");
            this.bestPagesMappings.Add("Potential", "Most Potential");
            ItemUtilExt itemUtilExt = CoreFactory.Instance.GetItemUtilExt();
            ReportDataProvider reportDataProvider = Sitecore.Configuration.Factory.CreateObject("reporting/dataProvider", true) as ReportDataProvider;
            this.contactsStatesDataSource = new Sitecore.Support.Modules.EmailCampaign.Statistics.DataSources.ContactsStatesDataSource(EcmFactory.GetDefaultFactory(), CoreFactory.Instance, new ReportDataProviderExt(reportDataProvider, itemUtilExt), RecipientRepository.GetDefaultInstance());
        }

        private MessageReportResponse<ContactsStatesReportModel> ContactsStatesReportResponse(string messageId, string[] automationStates, string language, DataPage page, int utcOffset)
        {
            MessageReportResponse<ContactsStatesReportModel> response = new MessageReportResponse<ContactsStatesReportModel>();
            try
            {
                Guid guid = new Guid(messageId);
                DataTable table = this.GetContactsStatesData(guid, automationStates, language, page);
                int num = page.Index * page.Size;
                int num2 = num + page.Size;
                if (num2 > (table.Rows.Count + num))
                {
                    num2 = table.Rows.Count + num;
                }
                int num3 = (table.Rows.Count < page.Size) ? table.Rows.Count : page.Size;
                List<ContactsStatesReportModel> list = new List<ContactsStatesReportModel>(num2 - num);
                for (int i = 0; i < num3; i++)
                {
                    DataRow row = table.Rows[i];
                    DateTime dateTime = (DateTime)row["Entry"];
                    ContactsStatesReportModel item = new ContactsStatesReportModel
                    {
                        Email = (string)row["Email"],
                        UtcEntry = dateTime,
                        FullName = row["FullName"] as string,
                        ItemId = Guid.NewGuid().ToString(),
                        StateName = (string)row["StateName"]
                    };
                    DateTime datetime = Util.ServerTimeToLocal(dateTime, utcOffset);
                    item.Entry = DateUtil.ToIsoDate(datetime);
                    list.Add(item);
                }
                response.Results = list.ToArray();
                DataPage page2 = new DataPage
                {
                    Size = page.Size,
                    Index = page.Index + 1
                };
                int num5 = this.GetContactsStatesData(guid, automationStates, language, page2).Rows.Count + num2;
                response.TotalCount = num5;
            }
            catch (Exception exception)
            {
                Log.Error(exception.Message, exception, this);
                response.Error = true;
                response.ErrorMessage = EcmTexts.Localize("A serious error occurred please contact the administrator", new object[0]);
            }
            return response;
        }

        private List<EmailLandingPagesReportModel> Convert(DataTable dataTable, DataPage page = null)
        {
            int num = (page != null) ? (page.Index * page.Size) : 0;
            int count = (page != null) ? (num + page.Size) : dataTable.Rows.Count;
            if (count > dataTable.Rows.Count)
            {
                count = dataTable.Rows.Count;
            }
            List<EmailLandingPagesReportModel> list = new List<EmailLandingPagesReportModel>(count - num);
            for (int i = num; i < count; i++)
            {
                EmailLandingPagesReportModel item = new EmailLandingPagesReportModel
                {
                    Attention = (long)dataTable.Rows[i]["Attention"],
                    ItemId = Guid.NewGuid().ToString(),
                    PageBounced = (long)dataTable.Rows[i]["PageBounced"],
                    Potential = Math.Round((decimal)dataTable.Rows[i]["Potential"], 1),
                    Url = (string)dataTable.Rows[i]["Url"],
                    Value = (long)dataTable.Rows[i]["Value"],
                    ValuePerVisit = Math.Round((decimal)dataTable.Rows[i]["ValuePerVisit"], 1),
                    Visits = (long)dataTable.Rows[i]["Visits"]
                };
                list.Add(item);
            }
            return list;
        }

        public DataTable GetContactsStatesData(Guid messageId, string[] automationStates, string language, DataPage page)
        {
            Assert.ArgumentNotNull(automationStates, "automationStates");
            MessageItem messageItem = Factory.Instance.GetMessageItem(messageId.ToString());
            if (messageItem == null)
            {
                throw new EmailCampaignException("No message item was found by the id '{0}'.", new object[] { messageId });
            }
            return this.contactsStatesDataSource.GetContactsStatesData(messageItem.PlanId.ToGuid(), automationStates, language, page);
        }

        private Response GetBestEmailLandingPagesReportResponse(string messageId, string language, SortParameter sortParameter)
        {
            MessageReportResponse<BestEmailLandingPagesReportModel> messageReportResponse = new MessageReportResponse<BestEmailLandingPagesReportModel>();
            try
            {
                DataTable dataTable = this.emailLandingPagesRepository.GetEmailLandingPagesData(new Guid(messageId), language, new SortParameter("Url", SortDirection.Asc));
                List<BestEmailLandingPagesReportModel> list = new List<BestEmailLandingPagesReportModel>(this.bestPagesMappings.Count);
                foreach (KeyValuePair<string, string> current in this.bestPagesMappings)
                {
                    BestEmailLandingPagesReportModel bestEmailLandingPagesReportModel = new BestEmailLandingPagesReportModel();
                    bestEmailLandingPagesReportModel.ItemId = Guid.NewGuid().ToString();
                    bestEmailLandingPagesReportModel.PerformanceIndicator = EcmTexts.Localize(current.Value, new object[0]);
                    bestEmailLandingPagesReportModel.PerformanceIndicatorColumn = current.Key;
                    if (dataTable.Rows.Count == 0)
                    {
                        bestEmailLandingPagesReportModel.EmailLandingPages = new EmailLandingPagesReportModel[0];
                        bestEmailLandingPagesReportModel.Url = "-";
                    }
                    else
                    {
                        dataTable.DefaultView.Sort = string.Format("{0} DESC", current.Key);
                        dataTable = dataTable.DefaultView.ToTable();
                        bestEmailLandingPagesReportModel.EmailLandingPages = this.Convert(dataTable, null).ToArray();
                        bestEmailLandingPagesReportModel.Url = (string)dataTable.Rows[0]["Url"];
                    }
                    list.Add(bestEmailLandingPagesReportModel);
                }
                if (sortParameter != null)
                {
                    if (sortParameter.Name == "performanceIndicator")
                    {
                        List<BestEmailLandingPagesReportModel> arg_1B5_0;
                        if (sortParameter.Direction != SortDirection.Desc)
                        {
                            arg_1B5_0 = (from model in list
                                         orderby model.PerformanceIndicator
                                         select model).ToList<BestEmailLandingPagesReportModel>();
                        }
                        else
                        {
                            arg_1B5_0 = (from model in list
                                         orderby model.PerformanceIndicator descending
                                         select model).ToList<BestEmailLandingPagesReportModel>();
                        }
                        list = arg_1B5_0;
                    }
                    if (sortParameter.Name == "url")
                    {
                        List<BestEmailLandingPagesReportModel> arg_223_0;
                        if (sortParameter.Direction != SortDirection.Desc)
                        {
                            arg_223_0 = (from model in list
                                         orderby model.Url
                                         select model).ToList<BestEmailLandingPagesReportModel>();
                        }
                        else
                        {
                            arg_223_0 = (from model in list
                                         orderby model.Url descending
                                         select model).ToList<BestEmailLandingPagesReportModel>();
                        }
                        list = arg_223_0;
                    }
                }
                messageReportResponse.Results = list.ToArray();
                messageReportResponse.TotalCount = list.Count;
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error(ex.Message, ex, this);
                messageReportResponse.Error = true;
                messageReportResponse.ErrorMessage = EcmTexts.Localize("A serious error occurred please contact the administrator", new object[0]);
            }
            return messageReportResponse;
        }

        private Response GetEmailBouncesDetailsReportResponse(string messageId, string language, DataPage page, int utcOffset)
        {
            string[] automationStates = new string[] { "Hard Bounce", "Soft Bounce" };
            return this.ContactsStatesReportResponse(messageId, automationStates, language, page, utcOffset);
        }

        private Response GetEmailLandingPagesReportResponse(string messageId, string language, SortParameter sortParameter, DataPage page)
        {
            if (sortParameter == null)
            {
                sortParameter = new SortParameter("Value", SortDirection.Desc);
            }
            MessageReportResponse<EmailLandingPagesReportModel> response = new MessageReportResponse<EmailLandingPagesReportModel>();
            try
            {
                DataTable dataTable = this.emailLandingPagesRepository.GetEmailLandingPagesData(new Guid(messageId), language, sortParameter);
                response.Results = this.Convert(dataTable, page).ToArray();
                response.TotalCount = dataTable.Rows.Count;
            }
            catch (Exception exception)
            {
                Log.Error(exception.Message, exception, this);
                response.Error = true;
                response.ErrorMessage = EcmTexts.Localize("A serious error occurred please contact the administrator", new object[0]);
            }
            return response;
        }

        private Response GetMessagePerformancePerLanguageReportResponse(string messageId)
        {
            MessageReportResponse<MessageStatistics> response = new MessageReportResponse<MessageStatistics>();
            try
            {
                List<MessageStatistics> messageStatistics = this.messageStatisticsService.GetMessageStatistics(new Guid(messageId));
                messageStatistics.RemoveAll(s => s.Language == "0");
                response.Results = messageStatistics.ToArray();
                response.TotalCount = messageStatistics.Count<MessageStatistics>();
            }
            catch (Exception exception)
            {
                Log.Error(exception.Message, exception, this);
                response.Error = true;
                response.ErrorMessage = EcmTexts.Localize("A serious error occurred please contact the administrator", new object[0]);
            }
            return response;
        }

        private Response GetUnsubscribedDetailsReportResponse(string messageId, string language, DataPage page, int utcOffset)
        {
            string[] automationStates = new string[] { "Unsubscribed", "Unsubscribed From All" };
            return this.ContactsStatesReportResponse(messageId, automationStates, language, page, utcOffset);
        }

        [ActionName("DefaultAction")]
        public Response MessageReport(MessageReportDataSourceContext data)
        {
            Assert.ArgumentNotNull(data, "requestArgs");
            DataPage page = new DataPage
            {
                Index = data.PageIndex,
                Size = data.PageSize
            };
            SortParameter sortParameter = this.sortParameterFactory.Create(data.Sorting);
            switch (data.Type)
            {
                case "{B25B894F-867D-4F49-A4D8-CFFFAB28C46D}":
                    return this.GetBestEmailLandingPagesReportResponse(data.MessageId, data.Language, sortParameter);

                case "{FCA32D8F-7D95-41E7-9F25-B69D620D1680}":
                    return this.GetEmailLandingPagesReportResponse(data.MessageId, data.Language, sortParameter, page);

                case "{5164EF31-7CF5-47A5-8B5B-2EDFB59FD93C}":
                    return this.GetUnsubscribedDetailsReportResponse(data.MessageId, data.Language, page, data.UtcOffset);

                case "{3C101F2E-2D9A-4A31-82A0-FAB3C979914B}":
                    return this.GetEmailBouncesDetailsReportResponse(data.MessageId, data.Language, page, data.UtcOffset);

                case "{B8736652-76C6-428E-840D-67E363E593F8}":
                    return this.GetMessagePerformancePerLanguageReportResponse(data.MessageId);
            }
            return new MessageReportResponse<object>
            {
                Results = new object[0],
                TotalCount = 0
            };
        }
    }
}
