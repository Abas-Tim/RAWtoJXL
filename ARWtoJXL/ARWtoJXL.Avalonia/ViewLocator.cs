using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace ARWtoJXL.Avalonia
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param is null)
                return new TextBlock { Text = "" };

            var name = param.GetType().Namespace!.Replace(".ViewModels", ".Views")
                + param.GetType().Name.Replace("ViewModel", "");
            var type = Type.GetType(name);
            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? value)
        {
            return value != null && value.GetType().Namespace != null && value.GetType().Namespace!.EndsWith(".ViewModels");
        }
    }
}
