﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using KModkit;
using System;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;

public class burgerAlarmScript : MonoBehaviour
{

    public KMBombModule Module;
    public KMBombInfo Info;
    public KMAudio Audio;
    public KMSelectable[] btns;
    public KMSelectable order, submit;
    public TextMesh numberText, timerText, numberUnderText, timerUnderText;
    public Transform[] textureTransforms;
    public GameObject x, check;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool solved;

    private int[] buttonSymbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }; // Which symbol is on [BUTTON #]?
    private readonly int[] symbolPositions = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }; // Which button is [SYMBOL #] on?
    private static readonly string[] symbolNames = { "mayo", "bun", "tomato", "cheese", "lettuce", "onions", "pickles", "mustard", "ketchup", "meat" };
    //                                                 0       1      2         3         4          5         6          7          8          9
    private readonly int[] number = { 10, 10, 10, 10, 10, 10, 10 };

    private static readonly int[,] table =
    {
        { 0, 6, 3, 4, 8, 5, 0, 6, 5, 5 },
        { 5, 1, 0, 6, 8, 1, 7, 7, 5, 6 },
        { 6, 2, 3, 2, 9, 4, 3, 8, 5, 1 },
        { 8, 8, 3, 8, 3, 9, 2, 2, 6, 7 },
        { 6, 9, 9, 1, 7, 9, 8, 2, 4, 1 },
        { 4, 9, 8, 2, 0, 8, 0, 5, 0, 9 },
        { 9, 1, 1, 1, 9, 6, 2, 7, 5, 3 },
        { 1, 7, 3, 6, 0, 0, 0, 0, 4, 2 },
        { 5, 4, 1, 9, 2, 7, 2, 3, 4, 7 },
        { 3, 8, 4, 7, 6, 3, 7, 4, 5, 4 }
    };

    private static readonly string[] primes = { "2", "3", "5", "7" };
    private bool currentlyOrdering = false, finishedIncreasing = false;

    private readonly int[] rowOrders = { 0, 0, 0, 0, 0 };
    private readonly int[] colOrders = { 0, 0, 0, 0, 0 };

    private readonly string[] orderStrings = { "", "", "", "", "" };
    private int shownOrder = 0;
    private readonly int[] btnsToPress = { 1, 0, 0, 0, 0, 0, 1 };
    private int btnsPressed = 0;
    private bool sequenceCorrect = true;
    private bool cooldown = false;
    private readonly string[] reasonsForStrike = { "", "", "", "", "", "", "" };
    private Coroutine time;

    private readonly int[] swaps = { 10, 10, 10, 10, 10, 10, 10, 10 };

    private readonly int[] rows = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    private readonly int[] cols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    private bool isActivated;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < 10; i++)
            textureTransforms[i].gameObject.SetActive(false);
        x.SetActive(false);
        check.SetActive(false);
        Module.OnActivate += Activate;
        order.OnInteract += delegate ()
        {
            if (!solved && !cooldown && isActivated)
                Order();
            order.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, order.transform);
            return false;
        };

        submit.OnInteract += delegate ()
        {
            if (!solved && !cooldown && isActivated)
                Submit();
            submit.AddInteractionPunch(10);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, submit.transform);
            return false;
        };

        for (int i = 0; i < btns.Length; i++)
        {
            int j = i;

            btns[i].OnInteract += delegate ()
            {
                if (!solved && !cooldown && isActivated)
                    BtnPress(j);
                btns[j].AddInteractionPunch();
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, btns[j].transform);
                return false;
            };
        }
    }

    private void Activate()
    {
        isActivated = true;
        for (int i = 0; i < 10; i++)
            textureTransforms[i].gameObject.SetActive(true);
        x.SetActive(true);
        check.SetActive(true);
        GenerateModule();
    }

    private void GenerateModule()
    {
        string[] modules = Info.GetModuleNames().ToArray();

        // Randomize buttons.
        buttonSymbols = buttonSymbols.Shuffle().ToArray();
        var originalTransforms = textureTransforms.Select(tx => tx.transform.localPosition).ToArray();

        for (int i = 0; i < 10; i++)
        {
            textureTransforms[buttonSymbols[i]].transform.localPosition = originalTransforms[i];
        }

        for (int i = 0; i < 10; i++)
        {
            Debug.LogFormat("[Burger Alarm #{0}] Button #{1} has {2} on it.", _moduleId, i + 1, symbolNames[buttonSymbols[i]]);
            symbolPositions[buttonSymbols[i]] = i;
        }

        timerText.text = "";

        // Calculate answer.

        var tableOffsets = new int[8];

        // Number #1...

        if (primes.Contains(Info.GetSerialNumber()[5].ToString()))
        {
            if (buttonSymbols[3] == 8 || buttonSymbols[4] == 8 || buttonSymbols[5] == 8)
                tableOffsets[0] = 7;

            else
                tableOffsets[0] = 4;
        }

        else
        {
            if (buttonSymbols[2] == 3 || buttonSymbols[5] == 3 || buttonSymbols[8] == 3)
                tableOffsets[0] = 6;

            else
                tableOffsets[0] = 3;
        }

        // Number #2...

        if (modules.Contains("The Clock") || modules.Contains("Rubik’s Clock"))
        {
            if (symbolPositions[6] / 3 == symbolPositions[0] / 3)
                tableOffsets[1] = 0;

            else
                tableOffsets[1] = 1;
        }

        else
        {
            if (symbolPositions[2] % 3 == symbolPositions[1] % 3 && symbolPositions[2] != 9 && symbolPositions[1] != 9)
                tableOffsets[1] = 8;

            else if ((symbolPositions[1] % 3 == 1 && symbolPositions[2] == 9) || (symbolPositions[2] % 3 == 1 && symbolPositions[1] == 9))
                tableOffsets[1] = 8;

            else
                tableOffsets[1] = 2;
        }

        // Number #3...

        if (Info.GetBatteryCount(Battery.D) == 0)
        {
            if (symbolPositions[7] != 6 && symbolPositions[7] != 7 && symbolPositions[7] != 8)
                tableOffsets[2] = 5;

            else
                tableOffsets[2] = 9;
        }

        else
        {
            if (symbolPositions[9] % 3 != 1 && symbolPositions[9] != 9)
                tableOffsets[2] = 3;

            else
                tableOffsets[2] = 7;
        }

        // Number #4...

        if (Info.IsPortPresent(Port.HDMI) || Info.IsPortPresent(Port.PCMCIA))
        {
            if (symbolPositions[4] > 7)
                tableOffsets[3] = 1;

            else
                tableOffsets[3] = 0;
        }

        else
        {
            if (FindAdjacentBtns(6).Contains(symbolPositions[7]))
                tableOffsets[3] = 4;

            else
                tableOffsets[3] = 8;
        }

        // Number #5...

        if (Info.IsTwoFactorPresent())
        {
            if (symbolPositions[5] != 6 && symbolPositions[5] != 9)
                tableOffsets[4] = 8;

            else
                tableOffsets[4] = 3;
        }

        else
        {
            if (!FindAdjacentBtns(8).Contains(symbolPositions[0]))
                tableOffsets[4] = 6;

            else
                tableOffsets[4] = 9;
        }

        // Number #6...

        if (Info.IsIndicatorPresent(Indicator.NLL) || Info.IsIndicatorPresent(Indicator.SND))
        {
            if (symbolPositions[2] > 5)
                tableOffsets[5] = 1;

            else
                tableOffsets[5] = 0;
        }

        else
        {
            if (symbolPositions[1] < 6)
                tableOffsets[5] = 4;

            else
                tableOffsets[5] = 5;
        }

        // Number #7...

        if (Info.GetSerialNumber().Contains("B") || Info.GetSerialNumber().Contains("U") || Info.GetSerialNumber().Contains("R") || Info.GetSerialNumber().Contains("G") || Info.GetSerialNumber().Contains("3"))
        {
            if (symbolPositions[3] / 3 > symbolPositions[0] / 3)
                tableOffsets[6] = 5;

            else
                tableOffsets[6] = 9;
        }

        else
        {
            if (symbolPositions[8] / 3 < symbolPositions[1] / 3)
                tableOffsets[6] = 3;

            else
                tableOffsets[6] = 7;
        }

        // Number #8...

        if (modules.Contains("Ice Cream") || modules.Contains("Cooking") || modules.Contains("Cookie Jars") || modules.Contains("Pie"))
        {
            if ((symbolPositions[7] % 3 < symbolPositions[9] % 3 && symbolPositions[7] != 9 && symbolPositions[9] != 9) ||
                (symbolPositions[7] % 3 == 0 && symbolPositions[9] == 9) ||
                (symbolPositions[9] % 3 == 2 && symbolPositions[7] == 9))
                tableOffsets[7] = 1;

            else
                tableOffsets[7] = 0;
        }

        else
        {
            if ((symbolPositions[1] % 3 > symbolPositions[4] % 3 && symbolPositions[1] != 9 && symbolPositions[4] != 9) || (symbolPositions[1] == 9 && symbolPositions[4] % 3 < 1) || (symbolPositions[4] == 9 && symbolPositions[1] % 3 > 1))
                tableOffsets[7] = 4;

            else
                tableOffsets[7] = 8;
        }

        numberText.text = "";

        for (int i = 0; i < 8; i++)
        {
            if (i != 7)
            {
                int rndNum = Random.Range(0, 10);
                swaps[i] = (tableOffsets[i] + rndNum) % 10;

                for (int x = 0; x < 10; x++)
                {
                    if (swaps.Contains((tableOffsets[i] + rndNum) % 10))
                    {
                        rndNum = (rndNum + 1) % 10;
                        swaps[i] = (tableOffsets[i] + rndNum) % 10;
                    }
                }

                number[i] = rndNum;
            }

            else
            {
                int rndNum = Random.Range(0, 10);
                swaps[i] = (tableOffsets[i] + rndNum) % 10;

                for (int j = 0; j < 10; j++)
                {
                    if (swaps.Contains((number[0] + number[1] + number[2] + number[3] + number[4] + number[5] + number[6] + tableOffsets[7]) % 10))
                    {
                        for (int x = 0; x < 7; x++)
                        {
                            number[x] = (number[x] + 1) % 10;
                        }

                        swaps[i] = (tableOffsets[i] + rndNum) % 10;
                    }
                }
            }

            Debug.LogFormat("[Burger Alarm #{0}] The answer from Table #{1} was {2}.", _moduleId, i + 1, tableOffsets[i]);
        }

        for (int i = 0; i < 7; i++)
        {
            numberText.text += number[i];

            swaps[i] = (tableOffsets[i] + number[i]) % 10;
        }

        swaps[7] = (tableOffsets[7] + number[0] + number[1] + number[2] + number[3] + number[4] + number[5] + number[6]) % 10;

        Debug.LogFormat("[Burger Alarm #{0}] The number on the module is {1}.", _moduleId, numberText.text);

        // Mess with table

        int swappedThing;

        rows[swaps[0]] = swaps[1];
        rows[swaps[1]] = swaps[0];
        Debug.LogFormat("[Burger Alarm #{0}] Swapping rows {1} and {2}.", _moduleId, swaps[0], swaps[1]);

        cols[swaps[2]] = swaps[3];
        cols[swaps[3]] = swaps[2];
        Debug.LogFormat("[Burger Alarm #{0}] Swapping columns {1} and {2}.", _moduleId, swaps[2], swaps[3]);

        swappedThing = rows[swaps[4]];
        rows[swaps[4]] = rows[swaps[5]];
        rows[swaps[5]] = swappedThing;
        Debug.LogFormat("[Burger Alarm #{0}] Swapping rows {1} and {2}.", _moduleId, swaps[4], swaps[5]);

        swappedThing = cols[swaps[6]];
        cols[swaps[6]] = cols[swaps[7]];
        cols[swaps[7]] = swappedThing;
        Debug.LogFormat("[Burger Alarm #{0}] Swapping columns {1} and {2}.", _moduleId, swaps[6], swaps[7]);
    }

    private void Order()
    {
        if (!currentlyOrdering)
        {
            currentlyOrdering = true;
            finishedIncreasing = false;
            cooldown = true;

            Debug.LogFormat("[Burger Alarm #{0}] You pressed order!", _moduleId);

            time = StartCoroutine(Timer());

            // Generate order

            for (int i = 0; i < 5; i++)
            {
                rowOrders[i] = Random.Range(0, 10);
                colOrders[i] = Random.Range(0, 10);

                btnsToPress[i + 1] = table[rows[rowOrders[i]], cols[colOrders[i]]];
                orderStrings[i] = "no.    " + rowOrders[i] + colOrders[i];

                Debug.LogFormat("[Burger Alarm #{0}] Order #{1} is {2}.", _moduleId, i + 1, orderStrings[i].Replace("    ", " "));
                Debug.LogFormat("[Burger Alarm #{0}] That means you should press {1}.", _moduleId, symbolNames[btnsToPress[i + 1]]);
            }

            sequenceCorrect = true;

            for (int i = 0; i < 7; i++)
            {
                reasonsForStrike[i] = "";
            }
        }

        else if (finishedIncreasing)
        {
            StartCoroutine(ChangeOrder());
        }
    }

    private void Submit()
    {
        Debug.LogFormat("[Burger Alarm #{0}] You pressed submit!", _moduleId);

        if (!currentlyOrdering)
        {
            Module.HandleStrike();
            Debug.LogFormat("[Burger Alarm #{0}] That ain't right, because...", _moduleId);
            Debug.LogFormat("[Burger Alarm #{0}] Nobody even ordered anything.", _moduleId);
            Debug.LogFormat("[Burger Alarm #{0}] STRIKE!!!", _moduleId);
            StopAllCoroutines();
            StartCoroutine(StrikeAnimation());

            int randomNumber = Random.Range(0, 5);
            string[] soundNames = { "NoThisIsPatrick", "NumberFifteen", "ThisIsHowYouEatABigMac", "HamburgerPls", "MyDisappointmentBlaBlaBla" };

            Audio.PlaySoundAtTransform(soundNames[randomNumber], Module.transform);
        }

        else if (btnsPressed < 7)
        {
            Module.HandleStrike();
            Debug.LogFormat("[Burger Alarm #{0}] That ain't right, because...", _moduleId);
            for (int i = 0; i < 7; i++)
                if (reasonsForStrike[i] != "")
                    Debug.LogFormat("[Burger Alarm #{0}] {1}", _moduleId, reasonsForStrike[i]);

            Debug.LogFormat("[Burger Alarm #{0}] The burger's not big enough. The customer starves to death and you get fired.", _moduleId);
            Debug.LogFormat("[Burger Alarm #{0}] STRIKE!!!", _moduleId);
            StopAllCoroutines();
            StartCoroutine(StrikeAnimation());

            int randomNumber = Random.Range(0, 5);
            string[] soundNames = { "NoThisIsPatrick", "NumberFifteen", "ThisIsHowYouEatABigMac", "HamburgerPls", "MyDisappointmentBlaBlaBla" };

            Audio.PlaySoundAtTransform(soundNames[randomNumber], Module.transform);
        }

        else if (sequenceCorrect && currentlyOrdering)
        {
            Module.HandlePass();
            solved = true;

            Debug.LogFormat("[Burger Alarm #{0}] Looks like that was right. Module solved!", _moduleId);

            numberText.text = "no.    15";
            timerText.text = "GG";
            StopCoroutine(time);
            StartCoroutine(SolveFade());

            Audio.PlaySoundAtTransform("Solve", Module.transform);
        }

        else
        {
            Module.HandleStrike();
            Debug.LogFormat("[Burger Alarm #{0}] That ain't right, because...", _moduleId);
            for (int i = 0; i < 7; i++)
                if (reasonsForStrike[i] != "")
                    Debug.LogFormat("[Burger Alarm #{0}] {1}", _moduleId, reasonsForStrike[i]);

            Debug.LogFormat("[Burger Alarm #{0}] STRIKE!!!", _moduleId);
            StopAllCoroutines();
            StartCoroutine(StrikeAnimation());

            int randomNumber = Random.Range(0, 5);
            string[] soundNames = { "NoThisIsPatrick", "NumberFifteen", "ThisIsHowYouEatABigMac", "HamburgerPls", "MyDisappointmentBlaBlaBla" };

            Audio.PlaySoundAtTransform(soundNames[randomNumber], Module.transform);
        }

        currentlyOrdering = false;
        btnsPressed = 0;
    }

    private void BtnPress(int btnNum)
    {
        Debug.LogFormat("[Burger Alarm #{0}] You pressed the button with the {1}.", _moduleId, symbolNames[buttonSymbols[btnNum]]);

        if (btnsPressed >= 0 && btnsPressed <= 6)
        {
            if (btnsToPress[btnsPressed] != buttonSymbols[btnNum])
            {
                sequenceCorrect = false;

                if (btnsPressed == 0 || btnsPressed == 6)
                {
                    reasonsForStrike[0] = "These customers don't want none unless you got BUNS, hun!";
                }

                else
                {
                    reasonsForStrike[btnsPressed] = "Ingredient " + btnsPressed + " was wrong...";
                }
            }
        }

        else
        {
            sequenceCorrect = false;

            reasonsForStrike[6] = ("The burger's too big! Your customer has a heart attack and you're fired.");
        }

        btnsPressed++;
    }

    private IEnumerator SolveFade()
    {
        yield return new WaitForSeconds(0.5f);
        float fadeOutTime = 3f;
        Color originalColor = numberText.color;
        for (float t = 0.01f; t < fadeOutTime; t += Time.deltaTime)
        {
            numberText.color = Color.Lerp(originalColor, Color.clear, Mathf.Min(1, t / fadeOutTime));
            yield return null;
        }
    }

    private int[] FindAdjacentBtns(int symbolNum)
    {
        int[] adjacentBtns = { 10, 10, 10, 10 };

        if (symbolPositions[symbolNum] == 0)
        {
            adjacentBtns[0] = 1;
            adjacentBtns[1] = 3;
        }

        else if (symbolPositions[symbolNum] == 1)
        {
            adjacentBtns[0] = 0;
            adjacentBtns[1] = 2;
            adjacentBtns[2] = 4;
        }

        else if (symbolPositions[symbolNum] == 2)
        {
            adjacentBtns[0] = 1;
            adjacentBtns[1] = 5;
        }

        else if (symbolPositions[symbolNum] == 3)
        {
            adjacentBtns[0] = 0;
            adjacentBtns[1] = 4;
            adjacentBtns[2] = 6;
        }

        else if (symbolPositions[symbolNum] == 4)
        {
            adjacentBtns[0] = 1;
            adjacentBtns[1] = 3;
            adjacentBtns[2] = 5;
            adjacentBtns[3] = 7;
        }

        else if (symbolPositions[symbolNum] == 5)
        {
            adjacentBtns[0] = 2;
            adjacentBtns[1] = 4;
            adjacentBtns[2] = 8;
        }

        else if (symbolPositions[symbolNum] == 6)
        {
            adjacentBtns[0] = 3;
            adjacentBtns[1] = 7;
        }

        else if (symbolPositions[symbolNum] == 7)
        {
            adjacentBtns[0] = 4;
            adjacentBtns[1] = 6;
            adjacentBtns[2] = 8;
            adjacentBtns[3] = 9;
        }

        else if (symbolPositions[symbolNum] == 8)
        {
            adjacentBtns[0] = 7;
            adjacentBtns[1] = 5;
        }

        else
        {
            adjacentBtns[0] = 7;
        }

        return adjacentBtns;
    }

    private IEnumerator Timer()
    {
        int time = 0;
        shownOrder = 0;

        for (int i = 0; i < 100; i++)
        {
            timerText.text = time.ToString("00");

            time++;
            numberText.text = "";

            for (int x = 0; x < 7; x++)
            {
                numberText.text += Random.Range(0, 10).ToString();
            }

            yield return new WaitForSeconds(.005f);
        }

        finishedIncreasing = true;

        numberText.text = "HURRYUP";

        yield return new WaitForSeconds(1f);

        numberText.text = orderStrings[0];

        cooldown = false;

        yield return new WaitForSeconds(.5f);

        while (time != 0 && currentlyOrdering)
        {
            time--;
            timerText.text = time.ToString("00");

            yield return new WaitForSeconds(1f);
        }

        timerText.text = "";

        if (currentlyOrdering)
        {
            Module.HandleStrike();
            currentlyOrdering = false;
            btnsPressed = 0;
            Debug.LogFormat("[Burger Alarm #{0}] Your customer got impatient and left. STRIKE!!!", _moduleId);
            StopAllCoroutines();
            StartCoroutine(StrikeAnimation());

            int randomNumber = Random.Range(0, 5);
            string[] soundNames = { "NoThisIsPatrick", "NumberFifteen", "ThisIsHowYouEatABigMac", "HamburgerPls", "MyDisappointmentBlaBlaBla" };

            Audio.PlaySoundAtTransform(soundNames[randomNumber], Module.transform);
        }
    }

    private IEnumerator ChangeOrder()
    {
        numberText.text = "";

        yield return new WaitForSeconds(.25f);
        shownOrder = (shownOrder + 1) % 5;

        numberText.text = orderStrings[shownOrder];
    }

    private IEnumerator StrikeAnimation()
    {
        numberText.color = Color.red;
        numberUnderText.color = new Color32(50, 0, 0, 255);

        for (int i = 0; i < 100; i++)
        {
            yield return new WaitForSeconds(.01f);

            numberText.text = "";

            for (int x = 0; x < 7; x++)
                numberText.text += Random.Range(0, 10).ToString();

            timerText.text = Random.Range(0, 10).ToString() + Random.Range(0, 10).ToString();
        }

        numberText.color = new Color32(50, 225, 50, 255);
        numberUnderText.color = new Color32(0, 50, 0, 255);
        numberText.text = "";

        for (int i = 0; i < 7; i++)
        {
            numberText.text += number[i].ToString();
        }

        timerText.text = "";
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "Use !{0} order to press the order button, and !{0} submit to press submit. You can do !{0} press mayo bun tomato cheese lettuce onions pickles mustard ketchup meat to press buttons.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string cmd)
    {
        if (currentlyOrdering)
            while (cooldown) { yield return null; }

        if (cmd.ToLowerInvariant() == "order")
        {
            yield return null;
            yield return new KMSelectable[] { order };
        }

        else if (cmd.ToLowerInvariant() == "submit")
        {
            yield return null;
            yield return new KMSelectable[] { submit };
        }

        else if (cmd.ToLowerInvariant().StartsWith("press "))
        {
            string cmdBtns = cmd.Substring(6).ToLower();
            string[] btnSequence = cmdBtns.Split(' ');

            foreach (var btn in btnSequence)
            {
                if (!symbolNames.Contains(btn))
                {
                    yield return null;
                    yield return "sendtochaterror One of those isn't a button you can press...";
                    yield break;
                }
            }

            yield return null;
            foreach (var btn in btnSequence)
            {
                int ingredientNum = Array.IndexOf(symbolNames, btn);
                yield return new KMSelectable[] { btns[symbolPositions[ingredientNum]] };
            }
        }

        else
        {
            yield break;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!isActivated)
            yield return true;
        if (!currentlyOrdering)
            order.OnInteract();
        while (cooldown) { yield return null; }
        if (!sequenceCorrect)
        {
            Module.HandlePass();
            solved = true;

            numberText.text = "no.    15";
            timerText.text = "GG";
            StopCoroutine(time);
            StartCoroutine(SolveFade());

            Audio.PlaySoundAtTransform("Solve", Module.transform);
        }
        int start = btnsPressed;
        for (int i = start; i < btnsToPress.Length; i++)
        {
            btns[Array.IndexOf(buttonSymbols, Array.IndexOf(symbolNames, symbolNames[btnsToPress[i]]))].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        submit.OnInteract();
    }
}
