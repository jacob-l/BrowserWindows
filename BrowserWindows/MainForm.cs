using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using WebKit.DOM;

namespace BrowserWindows
{
    public partial class MainForm : Form
    {
        private readonly Dictionary<string, Action<NameValueCollection>> handlers;

        public MainForm()
        {
            InitializeComponent();

            handlers = new Dictionary<string, Action<NameValueCollection>>
            {
                { "callfromjs", nv => CallJs("showMessage", new object[] { nv["msg"] + " Ответ из С#" }) }
            };

            browser.Navigating += (sender, args) =>
                {
                    var url = args.Url;
                    if (url.Scheme != "mp")
                    {
                        //mp - myprotocol.
                        //Обрабатываем вызовы только нашего специального протокола.
                        //Переходы по обычным ссылкам работают как и прежде
                        return;
                    }

                    var parameters = System.Web.HttpUtility.ParseQueryString(url.Query);

                    handlers[url.Host.ToLower()](parameters);

                    //e.Cancel не работает! Поэтому отменим переход таким образом
                    browser.Navigate("#");
                };

            browser.DocumentText = @"
                <html>
                    <head>
                        
                    </head>
                    <body id=body>
                        <h1>Интерфейс</h1>
                        <button id=btn>Вызвать C#</button>
                        <p id=msg></p>

                        <script>
                            function jsCallHandler() {
                                // достаем и десериализуем данные из заданного div
                                var bufferDiv = document.getElementById('cs-js-buffer')
                                var dataFromCSharp = JSON.parse(bufferDiv.innerHTML);

                                return window[dataFromCSharp.functionName].apply(
                                    window,
                                    dataFromCSharp.arguments
                                );
                            }

                            function buttonClick() {
                                window.location.href = 'mp://callFromJs?msg=Сообщение из js.';
                            }
                            function showMessage(msg) {
                                document.getElementById('msg').innerHTML = msg;
                            }

                            document.getElementById('btn').onclick = buttonClick;
                        </script>
                    </body>
                </html>";
        }

        private void CallJs(string functionName, object[] arguments)
        {
            Invoke((MethodInvoker) delegate
            {
                var dict = new Dictionary<string, object>();
                dict["arguments"] = arguments;
                dict["functionName"] = functionName;

                //Передача параметров в InvokeScriptMethod не работает.
                //Поэтому устанавливаем имя js функции и аргументы внутри DIV в DOM.
                //Вызываем jsCallHandler, который извлечет информацию из DIV и вызовет указанную функцию.
                SetJsBuffer(dict);

                browser.Document.InvokeScriptMethod("jsCallHandler");
            });
        }

        /// <summary>
        /// Сериализует данные и устанавливает в DIV с идентификатором "cs-js-buffer".
        /// Используется чтобы передать параметры при вызове js функции из C#
        /// </summary>
        /// <param name="data">Данные, которые будут установлены</param>
        private void SetJsBuffer(object data)
        {
            const string id = "cs-js-buffer";

            Element elem = null;
            try
            {
                elem = browser.Document.GetElementById(id);
            }
            catch (Exception) { } // получим исключение, если элемент не найден

            if (elem == null)
            {
                elem = browser.Document.CreateElement("div");
                elem.SetAttribute("id", id);
                elem.SetAttribute("style", "display: none");
                browser.Document.GetElementsByTagName("body")[0].AppendChild(elem);
            }

            elem.TextContent = new JavaScriptSerializer().Serialize(data);
        }
    }
}
