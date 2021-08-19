using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using System.Text.RegularExpressions;

public class OrderPickingScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;
    public KMSelectable enterButton;
    public KMSelectable[] numberButtons, functionButtons;
    public TextMesh screenText;
    public MeshRenderer[] LEDs;
    public Material LEDsOnMat, LEDsOffMat;

    private static int _moduleIdCounter = 1;
    private int _moduleId, orderCount, orderNumber, currentOrder = 1, currentScreen = 0, productNeeded, productTotal, productRemain, productId, backspace, confirm, cancel;
    private int[] palletTotals = new[] { 231, 360, 96, 216, 256, 196, 110 }, edgework;
    private bool _moduleSolved, allowTyping = true;
    private string text, product, quantity, pallet, input = "";
    private string[] palletsArray = new[] { "CHEP", "SIPPL", "SLPR", "EWHITE", "ECHEP", "ESIPPL", "ESLPR" }, productsArray = new[] { "TT", "GC", "GP", "DN", "HK", "AX", "MM" };

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int i = 0; i < numberButtons.Length; i++)
        {
            int j = i;
            numberButtons[i].OnInteract += delegate ()
            {
                NumberButtonHandler(j);
                return false;
            };
        }

        var random = RuleSeedable.GetRNG();

        edgework = new[] {
            BombInfo.GetBatteryCount(),
            BombInfo.GetPortCount(),
            BombInfo.GetPortPlateCount(),
            BombInfo.GetBatteryHolderCount(),
            BombInfo.GetOnIndicators().Count(),
            BombInfo.GetOffIndicators().Count(),
            BombInfo.GetIndicators().Count()
        };

        if (random.Seed != 1)
        {
            for (int i = 0; i < palletTotals.Length; i++)
                palletTotals[i] = random.Next(76, 501);
            random.ShuffleFisherYates(edgework);
        }

        orderCount = edgework[0] % 3 + 1;

        int backNum = BombInfo.GetSerialNumber().Select(ch => ch >= '0' && ch <= '9' ? ch - '0' : ch - 'A' + 1).Sum();
        backspace = backNum % 3;

        int conNum = edgework[1] * edgework[2];
        if (conNum % 3 == backNum % 3)
        {
            conNum++;
        }
        confirm = conNum % 3;

        int canNum = 0;
        while (canNum == backspace || canNum == confirm)
        {
            canNum++;
            canNum %= 3;
        }
        cancel = canNum;

        Debug.LogFormat("[Order Picking #{0}] The confirm button is {1}.", _moduleId, functionButtons[confirm].name);
        Debug.LogFormat("[Order Picking #{0}] The cancel button is {1}.", _moduleId, functionButtons[cancel].name);
        Debug.LogFormat("[Order Picking #{0}] The backspace button is {1}.", _moduleId, functionButtons[backspace].name);

        Debug.LogFormat("[Order Picking #{0}] Orders needed: {1}.", _moduleId, orderCount);
        GenerateOrder();

        functionButtons[backspace].OnInteract += delegate ()
        {
            BackspaceHandler(backspace);
            return false;
        };

        functionButtons[confirm].OnInteract += delegate ()
        {
            ConfirmHandler(confirm);
            return false;
        };

        functionButtons[cancel].OnInteract += delegate ()
        {
            CancelHandler(cancel);
            return false;
        };

        enterButton.OnInteract = EnterButtonHandler;

        StartCoroutine(ScreenFlash());

        RenderScreen();
    }

    private void GenerateOrder()
    {
        Debug.LogFormat("[Order Picking #{0}] Order: {1}.", _moduleId, currentOrder);
        orderNumber = Rnd.Range(1000, 10000);
        Debug.LogFormat("[Order Picking #{0}] Order number: {1}.", _moduleId, orderNumber);
        pallet = palletsArray[Rnd.Range(0, palletsArray.Length)];
        Debug.LogFormat("[Order Picking #{0}] Pallet: {1}.", _moduleId, pallet);
        product = productsArray[Rnd.Range(0, productsArray.Length)];
        productId = Rnd.Range(1000, 10000);
        Debug.LogFormat("[Order Picking #{0}] Product: {1}-{2}.", _moduleId, product, productId);
        productNeeded = (orderNumber + productId) % 400;
        switch (pallet)
        {
            case "CHEP":
                productTotal = palletTotals[0];
                break;

            case "SIPPL":
                productTotal = palletTotals[1];
                break;

            case "SLPR":
                productTotal = palletTotals[2];
                break;

            case "EWHITE":
                productTotal = palletTotals[3];
                break;

            case "ECHEP":
                productTotal = palletTotals[4];
                break;

            case "ESIPPL":
                productTotal = palletTotals[5];
                break;

            case "ESLPR":
                productTotal = palletTotals[6];
                break;
        }
        Debug.LogFormat("[Order Picking #{0}] Product total: {1}.", _moduleId, productTotal);
        while (productNeeded > productTotal)
        {
            if (BombInfo.GetOnIndicators().Count() > 0)
                productNeeded -= 75;
            else
                productNeeded -= 50;
        }
        Debug.LogFormat("[Order Picking #{0}] Product needed: {1}.", _moduleId, productNeeded);
        productRemain = productTotal - productNeeded;
        Debug.LogFormat("[Order Picking #{0}] Product Remaining: {1}.", _moduleId, productRemain);
    }

    private void RenderScreen()
    {
        switch (currentScreen)
        {
            case 0:
                text = "Order: " + orderNumber + "Confirm - ?Cancel - ?";
                screenText.text = CharacterWrapping(text);
                break;
            case 1:
                text = "Pick onto:\n" + pallet + "\n\nPress ENTER";
                screenText.text = text;
                break;
            case 2:
                text = "Product:\n" + product + "-" + productId + "\n\nQuantity\nneeded:\n???\n\nConfirm - ?";
                screenText.text = text;
                break;
            case 3:
                text = "Confirm    remaining: ";
                screenText.text = CharacterWrapping(text);
                break;
        }
    }

    private IEnumerator ScreenFlash()
    {
        while (!_moduleSolved)
        {
            while (currentScreen == 3 && allowTyping)
            {
                screenText.text = CharacterWrapping(text + "█");
                yield return null;
            }
            yield return null;
        }
    }

    private string CharacterWrapping(string txt)
    {
        var list = new List<string>();
        while (txt.Length > 11)
        {
            list.Add(txt.Substring(0, 11));
            txt = txt.Substring(11);
        }
        list.Add(txt);
        return list.Join("\n");
    }

    private void NumberButtonHandler(int num)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, numberButtons[num].transform);
        numberButtons[num].AddInteractionPunch(0.5f);
        if (allowTyping && !_moduleSolved)
        {
            if (text.Length < 180)
            {
                text += num.ToString();
                if (currentScreen == 3)
                {
                    input += num;
                }
            }
        }
    }

    private bool EnterButtonHandler()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, enterButton.transform);
        enterButton.AddInteractionPunch(0.5f);
        if (!_moduleSolved)
        {
            switch (currentScreen)
            {
                case 1:
                    currentScreen = 2;
                    RenderScreen();
                    break;
                case 3:
                    if (input == productRemain.ToString())
                    {
                        input = "";
                        currentScreen = 0;
                        LEDs[currentOrder - 1].material = LEDsOnMat;
                        currentOrder++;
                        if (currentOrder != orderCount + 1)
                        {
                            GenerateOrder();
                        }
                        RenderScreen();
                    }
                    else
                    {
                        Debug.LogFormat("[Order Picking #{0}] Remaining quantity entered was {1} but should have been {2}. Strike!", _moduleId, input, productRemain);
                        StartCoroutine(Strike());
                    }
                    break;
                default:
                    break;
            }
        }
        return false;
    }

    private bool BackspaceHandler(int ix)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, functionButtons[ix].transform);
        functionButtons[ix].AddInteractionPunch(0.5f);
        if (allowTyping && !_moduleSolved && input != "")
        {
            if (currentScreen == 3)
            {
                Debug.Log(input);
                input = input.Substring(0, input.Length - 1);
                text = text.Substring(0, text.Length - (input.Length + 1)) + input;
            }
            else
            {
                StartCoroutine(Strike());
                Debug.LogFormat("[Order Picking #{0}] Backspace button was pressed incorrectly. Strike!", _moduleId);
            }
        }
        return false;
    }

    private bool ConfirmHandler(int ix)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, functionButtons[ix].transform);
        functionButtons[ix].AddInteractionPunch(0.5f);
        if (allowTyping && !_moduleSolved)
        {
            if (currentScreen == 0)
            {
                if (currentOrder == orderCount + 1)
                {
                    StartCoroutine(Strike());
                }
                else
                {
                    currentScreen = 1;
                    RenderScreen();
                }
            }
            else if (currentScreen == 2)
            {
                currentScreen = 3;
                RenderScreen();
            }
            else
            {
                StartCoroutine(Strike());
                Debug.LogFormat("[Order Picking #{0}] Confirm button was pressed incorrectly. Strike!", _moduleId);
            }
        }
        return false;
    }

    private bool CancelHandler(int ix)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, functionButtons[ix].transform);
        functionButtons[ix].AddInteractionPunch(0.5f);
        if (allowTyping && !_moduleSolved)
        {
            if (currentScreen == 0 && currentOrder == orderCount + 1)
                StartCoroutine(Pass());
            else
            {
                StartCoroutine(Strike());
                Debug.LogFormat("[Order Picking #{0}] Cancel button was pressed incorrectly. Strike!", _moduleId);
            }
        }
        return false;
    }

    private IEnumerator Pass()
    {
        Module.HandlePass();
        _moduleSolved = true;
        allowTyping = false;
        var txt = CharacterWrapping("Good job!  Now go home(:");
        screenText.text = "";
        yield return new WaitForSeconds(1f);
        for (int i = 0; i < txt.Length; i++)
        {
            screenText.text += txt[i];
            yield return new WaitForSeconds(0.1f);
        }
        for (int i = 0; i < LEDs.Length; i++)
        {
            LEDs[i].material = LEDsOffMat;
        }
        yield return null;
    }

    private IEnumerator Strike()
    {
        Module.HandleStrike();
        allowTyping = false;
        var txt = "Please see\na Team\nLeader so\nthey can\nrevert your\nmistake.";
        screenText.text = "";
        yield return new WaitForSeconds(1f);
        for (int i = 0; i < txt.Length; i++)
        {
            screenText.text += txt[i];
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(5f);
        currentScreen = 0;
        input = "";
        RenderScreen();
        allowTyping = true;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press <button> [buttons are: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, F1, F2, F3, Enter]";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        while (!allowTyping)
            yield return true;
        command = command.ToLowerInvariant();
        if (command.StartsWith("press ")) command = command.Substring(6);
        else
        {
            yield return "sendtochaterror Button presses must start with press.";
            yield break;
        }

        string[] list = command.Split(' ');
        for (int i = 0; i < list.Length; i++)
        {
            switch (list[i])
            {
                case "0":
                    numberButtons[0].OnInteract();
                    break;
                case "1":
                    numberButtons[1].OnInteract();
                    break;
                case "2":
                    numberButtons[2].OnInteract();
                    break;
                case "3":
                    numberButtons[3].OnInteract();
                    break;
                case "4":
                    numberButtons[4].OnInteract();
                    break;
                case "5":
                    numberButtons[5].OnInteract();
                    break;
                case "6":
                    numberButtons[6].OnInteract();
                    break;
                case "7":
                    numberButtons[7].OnInteract();
                    break;
                case "8":
                    numberButtons[8].OnInteract();
                    break;
                case "9":
                    numberButtons[9].OnInteract();
                    break;
                case "f1":
                    functionButtons[0].OnInteract();
                    break;
                case "f2":
                    functionButtons[1].OnInteract();
                    break;
                case "f3":
                    functionButtons[2].OnInteract();
                    break;
                case "enter":
                    enterButton.OnInteract();
                    break;
            }
            yield return new WaitForSeconds(0.1f);
        }
        yield return null;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!allowTyping)
            yield return true;
        while (currentOrder < orderCount + 1)
        {
            if (currentScreen == 0)
            {
                functionButtons[confirm].OnInteract();
                yield return new WaitForSeconds(0.25f);
            }
            if (currentScreen == 1)
            {
                enterButton.OnInteract();
                yield return new WaitForSeconds(0.25f);
            }
            if (currentScreen == 2)
            {
                functionButtons[confirm].OnInteract();
                yield return new WaitForSeconds(0.25f);
            }
            if (currentScreen == 3)
            {
                while (input != "")
                {
                    functionButtons[backspace].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
                var tmp = productRemain.ToString();
                foreach (var chr in tmp)
                {
                    numberButtons[chr - '0'].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
                enterButton.OnInteract();
                yield return new WaitForSeconds(0.25f);
            }
        }
        functionButtons[cancel].OnInteract();

        yield return null;
    }
}