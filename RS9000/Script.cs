﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

using CitizenFX.Core;
using CitizenFX.Core.Native;

using Newtonsoft.Json.Linq;

namespace RS9000
{
    internal class Script : BaseScript
    {
        private readonly Radar Radar = new Radar();
        private readonly Controller controller;

        private bool IsDisplayingKeyboard { get; set; }

        public const string Units = "mph";

        public Script()
        {
            controller = new Controller(this, Radar);

            Tick += Update;
            Tick += CheckInputs;
        }

        public void RegisterEventHandler(string eventName, Delegate callback)
        {
            EventHandlers[eventName] += callback;
        }

        public void RegisterNUICallback(string msg, Action<IDictionary<string, object>, CallbackDelegate> callback)
        {
            API.RegisterNuiCallbackType(msg);
            EventHandlers[$"__cfx_nui:{msg}"] += new Action<ExpandoObject, CallbackDelegate>((body, result) =>
            {
                callback?.Invoke(body, result);
            });
        }

        public static Vehicle GetVehicleDriving(Ped ped)
        {
            Vehicle v = ped.CurrentVehicle;
            bool driving = ped.SeatIndex == VehicleSeat.Driver;

            if (v == null || !driving || v.ClassType != VehicleClass.Emergency)
            {
                return null;
            }

            return v;
        }

        private bool InEmergencyVehicle => GetVehicleDriving(Game.PlayerPed) != null;

        private async Task CheckInputs()
        {
            if (Radar.IsDisplayed && !InEmergencyVehicle)
            {
                Radar.IsDisplayed = false;
            }
            else if (InEmergencyVehicle && !Radar.IsDisplayed && Radar.IsEnabled)
            {
                Radar.IsDisplayed = true;
            }

            if (Game.IsControlJustPressed(0, Control.VehicleDuck) && InEmergencyVehicle)
            {
                controller.Visible = !controller.Visible;
            }

            await Delay(0);
        }

        private async Task Update()
        {
            await Delay(10);

            Radar.Update();
        }

        public void ShowKeyboard(int limit, string text = "")
        {
            if (IsDisplayingKeyboard)
            {
                return;
            }

            API.DisplayOnscreenKeyboard(0, "", "", text, "", "", "", limit);

            Tick += KeyboardUpdate;
        }

        private Task KeyboardUpdate()
        {
            int status = API.UpdateOnscreenKeyboard();

            if (status == 1)
            {
                string result = API.GetOnscreenKeyboardResult();
                TriggerEvent("rs9000:_keyboardResult", result);
            }

            if (status == 1 || status == 2)
            {
                Tick -= KeyboardUpdate;
                IsDisplayingKeyboard = false;
            }

            return Task.FromResult(0);
        }

        public static void SendMessage(MessageType type, object data)
        {
            string json = JObject.FromObject(new { type, data }).ToString();
            API.SendNuiMessage(json);
        }

        public static float ConvertSpeedToMeters(float speed)
        {
            switch (Units)
            {
                case "mph":
                    speed /= 2.237f;
                    break;
                case "km/h":
                    speed /= 3.6f;
                    break;
                default:
                    throw new NotSupportedException("Units not supported");
            }
            return speed;
        }
    }
}
