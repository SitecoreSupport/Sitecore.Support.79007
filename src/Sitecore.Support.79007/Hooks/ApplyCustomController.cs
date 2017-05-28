namespace Sitecore.Support.Hooks
{
  using Sitecore.Configuration;
  using Sitecore.Diagnostics;
  using Sitecore.Events.Hooks;
  using Sitecore.SecurityModel;
  public class ApplyCustomController : IHook
  {

    public void Initialize()
    {
      using (new SecurityDisabler())
      {
        var databaseName = "core";
        string itemPath1 = "/sitecore/client/Applications/ECM/Pages/MessageReport/PageSettings/Tabs/Reports";

        var fieldName = "__Renderings";

        // protects from refactoring-related mistakes
        var type = typeof(Sitecore.Support.EmailCampaign.Server.Controllers.DataSource.MessageReportController);

        var typeName = type.FullName;
        var assemblyName = type.Assembly.GetName().Name;
        var fieldValue = $"EXM%2fFixedMessageReport";
        var oldFieldValue = $"EXM%2fMessageReport";

        var database = Factory.GetDatabase(databaseName);

        var item1 = database.GetItem(itemPath1);

        if (item1[fieldName].Contains(fieldValue))
        {
          // already installed
          return;
        }

        Log.Info($"Installing {assemblyName}", this);

        item1.Editing.BeginEdit();
        item1[fieldName] = item1[fieldName].Replace(oldFieldValue, fieldValue);
        item1.Editing.EndEdit();

      }

    }
  }
}
