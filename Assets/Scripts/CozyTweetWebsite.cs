using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// CozyTweet — Email Notification Controller
/// Attach this script to a GameObject (e.g. "NotifyManager") in your Unity scene.
///
/// REQUIRED SCENE REFERENCES:
///   EmailInputField   (TMP_InputField)
///   NotifyButton      (Button)
///   FeedbackText      (TMP_Text)
///
/// SETUP — TWO SERVICES REQUIRED:
///
///  1. FORMSPREE (stores the signup on your end)
///     → Sign up free at https://formspree.io
///     → Create a form and paste the endpoint below
///
///  2. EMAILJS (sends the confirmation email TO the subscriber)
///     → Sign up free at https://www.emailjs.com
///     → Add an Email Service (Gmail, Outlook, etc.)
///     → Create an Email Template with these variables:
///          {{to_email}}   — subscriber's email address
///          {{to_name}}    — friendly greeting name (defaults to "there")
///          {{reply_to}}   — your studio reply-to address
///       Example subject : "You're on the CozyTweet list! 🌸"
///       Example body    :
///          Hi {{to_name}}!
///          Thanks for signing up — you'll be the first to know
///          when our cute 3D assets land on the Unity Asset Store. 🐣
///          — The CozyTweet Team
///     → Copy your Service ID, Template ID, and Public Key into the Inspector
///
/// DUPLICATE DETECTION:
///   Registered emails are saved locally via PlayerPrefs so that if the same
///   browser/device submits again, a friendly message is shown immediately
///   without hitting either API. The key used is "CozyTweet_RegisteredEmails".
/// </summary>

public class CozyTweetWebsite : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  FORMSPREE — stores signups
    // ─────────────────────────────────────────────

    [Header("Formspree — Signup Storage")]
    [Tooltip("https://formspree.io/f/YOUR_FORM_ID")]
    [SerializeField] private string formspreeEndpoint = "https://formspree.io/f/YOUR_FORM_ID";

    // ─────────────────────────────────────────────
    //  EMAILJS — sends confirmation to subscriber
    // ─────────────────────────────────────────────

    [Header("EmailJS — Confirmation Email to Subscriber")]
    [Tooltip("Found in EmailJS dashboard → Email Services")]
    [SerializeField] private string emailjsServiceId = "YOUR_SERVICE_ID";

    [Tooltip("Found in EmailJS dashboard → Email Templates")]
    [SerializeField] private string emailjsTemplateId = "YOUR_TEMPLATE_ID";

    [Tooltip("Found in EmailJS dashboard → Account → Public Key")]
    [SerializeField] private string emailjsPublicKey = "YOUR_PUBLIC_KEY";

    [Tooltip("The reply-to address subscribers see when they reply (your studio email)")]
    [SerializeField] private string studioEmail = "hello@cozytweetstudio.com";

    // ─────────────────────────────────────────────
    //  UI REFERENCES
    // ─────────────────────────────────────────────

    [Header("UI References")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private Button notifyButton;
    [SerializeField] private TMP_Text feedbackText;

    // ─────────────────────────────────────────────
    //  FEEDBACK MESSAGES
    // ─────────────────────────────────────────────

    private const string MSG_EMPTY = "Please enter your email address";
    private const string MSG_INVALID = "Hmm, that doesn't look like a valid email";
    private const string MSG_DUPLICATE = "You're already on the list! We'll reach out when we launch";
    private const string MSG_SENDING = "Sending...";
    private const string MSG_SUCCESS = "You're on the list! Check your inbox for a confirmation";
    private const string MSG_ERROR = "Something went wrong — please try again";

    [Header("Feedback Fade Settings")]
    [Tooltip("Seconds the message stays fully visible before starting to fade")]
    [SerializeField] private float fadeDelay = 3f;
    [Tooltip("Duration of the fade out in seconds")]
    [SerializeField] private float fadeDuration = 1.2f;

    // ─────────────────────────────────────────────
    //  PLAYERPREFS KEY & LOCAL REGISTRY
    // ─────────────────────────────────────────────

    private const string PREFS_KEY = "CozyTweet_RegisteredEmails";

    // In-memory set for fast lookups during the session
    private HashSet<string> _registeredEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private Coroutine _fadeCoroutine;

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    private void Start()
    {
        LoadRegisteredEmails();

        // On WebGL, browsers throw InvalidStateError when Unity calls
        // setSelectionRange() on <input type="email"> (email inputs don't
        // support selection). Forcing Standard content type makes Unity
        // render <input type="text"> instead, which supports it fully.
#if UNITY_WEBGL
        if (emailInputField != null)
            emailInputField.contentType = TMP_InputField.ContentType.Standard;
#endif

        if (emailInputField != null)
            emailInputField.onSubmit.AddListener(_ => OnNotifyClicked());

        if (notifyButton != null)
            notifyButton.onClick.AddListener(OnNotifyClicked);

        ClearFeedback();
    }

    // ─────────────────────────────────────────────
    //  BUTTON HANDLER
    // ─────────────────────────────────────────────

    public void OnNotifyClicked()
    {
        string email = emailInputField != null ? emailInputField.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(email))
        {
            ShowFeedback(MSG_EMPTY, isError: true);
            return;
        }

        if (!IsValidEmail(email))
        {
            ShowFeedback(MSG_INVALID, isError: true);
            return;
        }

        // ── Duplicate check ────────────────────────
        if (_registeredEmails.Contains(email))
        {
            ShowFeedback(MSG_DUPLICATE, isError: false);
            return;
        }

        StartCoroutine(HandleSignup(email));
    }

    // ─────────────────────────────────────────────
    //  MAIN SIGNUP FLOW
    //  Step 1 — store in Formspree
    //  Step 2 — send confirmation email via EmailJS
    //  Step 3 — save email locally to detect duplicates
    // ─────────────────────────────────────────────

    private IEnumerator HandleSignup(string email)
    {
        SetInteractable(false);
        ShowFeedback(MSG_SENDING, isError: false, fade: false);

        // ── Step 1: Formspree ──────────────────────
        bool formspreeOk = false;
        yield return StartCoroutine(SubmitToFormspree(email, result => formspreeOk = result));

        if (!formspreeOk)
        {
            Debug.LogError("[CozyTweet] Formspree submission failed.");
            ShowFeedback(MSG_ERROR, isError: true, fade: true);
            SetInteractable(true);
            yield break;
        }

        // ── Step 2: EmailJS confirmation ───────────
        bool emailjsOk = false;
        yield return StartCoroutine(SendConfirmationEmail(email, result => emailjsOk = result));

        if (!emailjsOk)
        {
            // Signup stored — confirmation email failed silently
            Debug.LogWarning("[CozyTweet] EmailJS confirmation failed, but signup was stored.");
        }

        // ── Step 3: Save locally as registered ────
        SaveRegisteredEmail(email);

        if (emailInputField != null) emailInputField.text = string.Empty;
        SetInteractable(true);
        ShowFeedback(MSG_SUCCESS, isError: false, fade: true);
    }

    // ─────────────────────────────────────────────
    //  STEP 1 — FORMSPREE
    // ─────────────────────────────────────────────

    private IEnumerator SubmitToFormspree(string email, Action<bool> callback)
    {
        WWWForm form = new WWWForm();
        form.AddField("email", email);
        form.AddField("_subject", "CozyTweet — New Launch Notification Signup");
        form.AddField("message", $"New signup: {email} at {DateTime.UtcNow:u}");

        using UnityWebRequest request = UnityWebRequest.Post(formspreeEndpoint, form);
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success;
        if (!success)
            Debug.LogError($"[CozyTweet] Formspree error: {request.responseCode} — {request.error}");

        callback?.Invoke(success);
    }

    // ─────────────────────────────────────────────
    //  STEP 2 — EMAILJS CONFIRMATION TO SUBSCRIBER
    // ─────────────────────────────────────────────

    private IEnumerator SendConfirmationEmail(string subscriberEmail, Action<bool> callback)
    {
        string json = "{"
            + $"\"service_id\":\"{emailjsServiceId}\","
            + $"\"template_id\":\"{emailjsTemplateId}\","
            + $"\"user_id\":\"{emailjsPublicKey}\","
            + "\"template_params\":{"
            + $"\"to_email\":\"{subscriberEmail}\","
            + "\"to_name\":\"there\","
            + $"\"reply_to\":\"{studioEmail}\""
            + "}"
            + "}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request = new UnityWebRequest(
            "https://api.emailjs.com/api/v1.0/email/send", "POST"
        );
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("origin", "http://localhost");

        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success;
        if (!success)
            Debug.LogError($"[CozyTweet] EmailJS error: {request.responseCode} — {request.downloadHandler.text}");
        else
            Debug.Log($"[CozyTweet] Confirmation email sent to {subscriberEmail}");

        callback?.Invoke(success);
    }

    // ─────────────────────────────────────────────
    //  STEP 3 — LOCAL DUPLICATE REGISTRY
    //
    //  Emails are stored in PlayerPrefs as a single
    //  newline-separated string under PREFS_KEY.
    //  This persists across sessions on the same
    //  browser (WebGL uses IndexedDB under the hood).
    // ─────────────────────────────────────────────

    private void LoadRegisteredEmails()
    {
        _registeredEmails.Clear();

        string stored = PlayerPrefs.GetString(PREFS_KEY, string.Empty);
        if (string.IsNullOrEmpty(stored)) return;

        foreach (string entry in stored.Split('\n'))
        {
            string trimmed = entry.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                _registeredEmails.Add(trimmed);
        }

        Debug.Log($"[CozyTweet] Loaded {_registeredEmails.Count} registered email(s) from PlayerPrefs.");
    }

    private void SaveRegisteredEmail(string email)
    {
        _registeredEmails.Add(email);

        // Rebuild the stored string and persist
        string stored = string.Join("\n", _registeredEmails);
        PlayerPrefs.SetString(PREFS_KEY, stored);
        PlayerPrefs.Save();

        Debug.Log($"[CozyTweet] Saved {email} to local registry ({_registeredEmails.Count} total).");
    }

    // ─────────────────────────────────────────────
    //  FEEDBACK DISPLAY
    // ─────────────────────────────────────────────

    private void ShowFeedback(string message, bool isError, bool fade = true)
    {
        if (feedbackText == null) return;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        // Lavender for success/info, pink-red for errors
        Color baseColor = isError
            ? new Color(0.90f, 0.40f, 0.55f)   // #e5667f
            : new Color(0.61f, 0.49f, 0.78f);   // #9b7ec8

        feedbackText.gameObject.SetActive(true);
        feedbackText.text = message;
        feedbackText.color = baseColor;

        if (fade)
            _fadeCoroutine = StartCoroutine(FadeOutFeedback(baseColor));
    }

    private IEnumerator FadeOutFeedback(Color baseColor)
    {
        yield return new WaitForSeconds(fadeDelay);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            feedbackText.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        feedbackText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        feedbackText.gameObject.SetActive(false);
        _fadeCoroutine = null;
    }

    private void ClearFeedback()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (feedbackText == null) return;
        feedbackText.text = string.Empty;
        feedbackText.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    private void SetInteractable(bool interactable)
    {
        if (notifyButton != null) notifyButton.interactable = interactable;
        if (emailInputField != null) emailInputField.interactable = interactable;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        int atIndex = email.IndexOf('@');
        if (atIndex <= 0) return false;
        int dotIndex = email.LastIndexOf('.');
        return dotIndex > atIndex + 1 && dotIndex < email.Length - 1;
    }

    // ─────────────────────────────────────────────
    //  DEBUG — REGISTRY MANAGEMENT
    //  Wire these to Button OnClick events in the
    //  Inspector for easy testing. Disable or remove
    //  the buttons before publishing your final build.
    // ─────────────────────────────────────────────

    /// <summary>
    /// Removes a single email from the local PlayerPrefs registry.
    /// Wire to a Button's OnClick in the Inspector, or call from another script:
    ///   FindObjectOfType<CozyTweetWebsite>().RemoveRegisteredEmail("test@email.com");
    /// </summary>
    public void RemoveRegisteredEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            Debug.LogWarning("[CozyTweet] RemoveRegisteredEmail: no email provided.");
            return;
        }

        if (_registeredEmails.Remove(email.Trim()))
        {
            string stored = string.Join("\n", _registeredEmails);
            PlayerPrefs.SetString(PREFS_KEY, stored);
            PlayerPrefs.Save();
            Debug.Log($"[CozyTweet] Removed '{email}' from local registry. ({_registeredEmails.Count} remaining)");
        }
        else
        {
            Debug.LogWarning($"[CozyTweet] '{email}' was not found in local registry.");
        }
    }

    /// <summary>
    /// Wipes ALL registered emails from PlayerPrefs entirely.
    /// Wire to a Button's OnClick in the Inspector for quick test resets.
    /// </summary>
    public void ClearAllRegisteredEmails()
    {
        int count = _registeredEmails.Count;
        _registeredEmails.Clear();
        PlayerPrefs.DeleteKey(PREFS_KEY);
        PlayerPrefs.Save();
        Debug.Log($"[CozyTweet] Cleared all {count} registered email(s) from local registry.");
    }

    /// <summary>
    /// Prints all currently registered emails to the Console.
    /// Useful to verify what's stored without opening DevTools.
    /// </summary>
    public void PrintRegisteredEmails()
    {
        if (_registeredEmails.Count == 0)
        {
            Debug.Log("[CozyTweet] Local registry is empty.");
            return;
        }

        Debug.Log($"[CozyTweet] {_registeredEmails.Count} registered email(s):");
        foreach (string email in _registeredEmails)
            Debug.Log($"  → {email}");
    }
}