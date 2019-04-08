using ConnectApp.constants;
using ConnectApp.models;
using ConnectApp.plugins;
using ConnectApp.redux;
using Unity.UIWidgets.engine;
using Unity.UIWidgets.Redux;
using Unity.UIWidgets.ui;
using Unity.UIWidgets.widgets;
using UnityEngine;

namespace ConnectApp.canvas {
    public sealed class ConnectAppCanvas : UIWidgetsPanel {
        protected override void OnEnable()
        {
            base.OnEnable();
            Application.targetFrameRate = 60;
            FontManager.instance.addFont(Resources.Load<Font>("Material Icons"), familyName: "Material Icons");
            FontManager.instance.addFont(Resources.Load<Font>("Roboto-Regular"), familyName: "Roboto-Regular");
            FontManager.instance.addFont(Resources.Load<Font>("Roboto-Medium"), familyName: "Roboto-Medium");
            FontManager.instance.addFont(Resources.Load<Font>("Roboto-Bold"), familyName: "Roboto-Bold");
            FontManager.instance.addFont(Resources.Load<Font>("PingFangSC-Regular"), familyName: "PingFangSC-Regular");
            FontManager.instance.addFont(Resources.Load<Font>("PingFangSC-Medium"), familyName: "PingFangSC-Medium");
            FontManager.instance.addFont(Resources.Load<Font>("PingFangSC-Semibold"),
                familyName: "PingFangSC-Semibold");
            WechatPlugin.instance.init(Config.wechatAppId);
        }

        protected override Widget createWidget() {
            return new StoreProvider<AppState>(
                StoreProvider.store,
                new WidgetsApp(
                    home: new Router(),
                    pageRouteBuilder: pageRouteBuilder
                )
            );
        }

        private PageRouteFactory pageRouteBuilder {
            get {
                return (settings, builder) =>
                    new PageRouteBuilder(
                        settings,
                        (context, animation, secondaryAnimation) => builder(context)
                    );
            }
        }
    }
}