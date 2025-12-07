using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UC
{

    public class MasterInputManager : MonoBehaviour
    {
        [SerializeField] private int maxPlayers = 2;

        Dictionary<int, int> playerDevices = new();
        Dictionary<int, PlayerInput> playerInputs = new();
        int lastUpdateFrame = -1;

        static MasterInputManager _Instance;
        static MasterInputManager Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = FindFirstObjectByType<MasterInputManager>();
                    _Instance._RefreshInput();
                }
                return _Instance;
            }
        }

        private void Awake()
        {
            if (_Instance == null)
            {
                _Instance = this;
            }
            else if (_Instance != this)
            {
                Destroy(this);
                return;
            }

            _RefreshInput();
        }

        private void OnEnable()
        {
            // Subscribe to the device change event
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad)
            {
                switch (change)
                {
                    case InputDeviceChange.Added:
                        {
                            // Check if someone is using the keyboard and upgrade him automatically
                            int playerId = GetKeyboardPlayer();
                            if (playerId != -1)
                            {
                                playerDevices[playerId] = device.deviceId;
                                _SetupInput(playerId);
                            }
                        }
                        break;
                    case InputDeviceChange.Removed:
                    case InputDeviceChange.Disconnected:
                        {
                            // Check if someone was using this device
                            int playerId = GetPlayerByDeviceId(device.deviceId);
                            if (playerId != -1)
                            {
                                // Someone was using, downgrade him to keyboard, if it's available
                                if (GetKeyboardPlayer() == -1)
                                {
                                    playerDevices[playerId] = -2;
                                    _SetupInput(playerId);
                                }
                            }
                        }
                        break;
                    case InputDeviceChange.Reconnected:
                        //Debug.Log($"Gamepad reconnected: {device.displayName}");
                        break;
                    default:
                        //Debug.Log($"Gamepad state changed: {change}");
                        break;
                }
            }
        }

        void _RefreshInput()
        {
            if (lastUpdateFrame == Time.frameCount) return;
            lastUpdateFrame = Time.frameCount;

            Debug.Log("Refreshing input systems...");

            playerDevices = new();

            // Find the gamepad input with smaller ID, fallback to keyboard if gamepad is not found
            int minGamepadId = -1;

            for (int i = 0; i < maxPlayers; i++)
            {
                if (!FindGamepad(minGamepadId, out int deviceId))
                {
                    if (GetKeyboardPlayer() == -1)
                    {
                        // Device not found, set device to keyboard
                        deviceId = -2;
                    }
                }
                else
                {
                    minGamepadId = deviceId;
                }
                if (deviceId != -1)
                {
                    playerDevices.Add(i, deviceId);
                }
            }
        }

        bool _SetupInput(int playerId, PlayerInput playerInput = null)
        {
            if (playerInput == null)
            {
                if (!playerInputs.TryGetValue(playerId, out playerInput)) return false;
            }

            if (playerInput.enabled)
            {
                return ActualSetupInput(playerId, playerInput);
            }
            else
            {
                StartCoroutine(ActualSetupInputCR(playerId, playerInput));
            }

            return true;
        }

        IEnumerator ActualSetupInputCR(int playerId, PlayerInput playerInput)
        {
            playerInput.enabled = true;
            yield return null;
            ActualSetupInput(playerId, playerInput);
        }

        bool ActualSetupInput(int playerId, PlayerInput playerInput)
        {
            int deviceId = -1;

            try
            {
                playerInput.user.UnpairDevices();

                // Assign devices
                if (playerDevices.TryGetValue(playerId, out deviceId))
                {
                    if (deviceId != -2)
                    {
                        var device = GetDeviceById(deviceId);
                        if (device != null)
                        {
                            playerInput.SwitchCurrentControlScheme("Gamepad", device);
                            Debug.Log($"Assigned gamepad {device.displayName} (ID = {deviceId}) to player {playerId}");
                        }
                        else
                        {
                            Debug.Log($"Could not assign device to player {playerId} - device not found!");
                        }
                    }
                    else
                    {
                        playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", GetKBAndMouseDevices());
                        Debug.Log($"Assigned keyboard & mouse to player {playerId}");
                    }

                    playerInputs[playerId] = playerInput;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to setup input for player {playerId}, device = {deviceId}: {e.Message}");
                return false;
            }


            return true;
        }

        bool FindGamepad(int minId, out int minDeviceId)
        {
            minDeviceId = int.MaxValue;
            foreach (var device in Gamepad.all)
            {
                if ((device.deviceId < minDeviceId) && (device.deviceId > minId))
                {
                    minDeviceId = device.deviceId;
                }
            }

            return ((minDeviceId != minId) && (minDeviceId != int.MaxValue));
        }

        int GetKeyboardPlayer()
        {
            foreach (var p in playerDevices)
            {
                if (p.Value == -2) return p.Key;
            }

            return -1;
        }

        InputDevice GetDeviceById(int id)
        {
            foreach (var device in InputSystem.devices)
            {
                if (device.deviceId == id)
                {
                    return device;
                }
            }
            return null;
        }

        InputDevice[] GetKBAndMouseDevices()
        {
            var devices = InputSystem.devices;
            var ret = new List<InputDevice>();
            foreach (var d in devices)
            {
                if ((d.name.IndexOf("keyboard", StringComparison.OrdinalIgnoreCase) != -1) ||
                    (d.name.IndexOf("mouse", StringComparison.OrdinalIgnoreCase) != -1))
                {
                    ret.Add(d);
                }
            }

            return ret.ToArray();
        }


        int GetPlayerByDeviceId(int deviceId)
        {
            foreach (var p in playerDevices)
            {
                if (p.Value == deviceId) return p.Key;
            }

            return -1;
        }

        public static void SetupInput(int playerId, PlayerInput playerInput)
        {
            Instance?._SetupInput(playerId, playerInput);
        }

        public static int GetMaxPlayers()
        {
            return Instance?.maxPlayers ?? 4;
        }
    }
}