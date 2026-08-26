using Microsoft.AspNetCore.Components;

namespace BlazorClient.Components;

public partial class IconPicker
{
    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public bool ShowInput { get; set; } = true;

    private bool IsModalOpen { get; set; } = false;
    private string SearchTerm { get; set; } = string.Empty;
    private string ActiveCategory { get; set; } = "All";

    private List<string> AllIcons => SimpleLineIcons.Concat(Ionicons).ToList();

    private IEnumerable<string> FilteredIcons
    {
        get
        {
            var icons = ActiveCategory switch
            {
                "Simple" => SimpleLineIcons,
                "Ionicons" => Ionicons,
                _ => AllIcons
            };

            if (string.IsNullOrWhiteSpace(SearchTerm))
                return icons;

            return icons.Where(i => i.ToLower().Contains(SearchTerm.ToLower()));
        }
    }

    private void OpenModal()
    {
        IsModalOpen = true;
        SearchTerm = string.Empty;
    }

    private void CloseModal()
    {
        IsModalOpen = false;
    }

    private async Task SelectIcon(string icon)
    {
        Value = icon;
        await ValueChanged.InvokeAsync(Value);
        CloseModal();
    }

    private void SetCategory(string category)
    {
        ActiveCategory = category;
    }

    private string FormatIconName(string iconClass)
    {
        return iconClass.Replace("icon-", "").Replace("ion-", "").Replace("social-", "");
    }

    private static readonly List<string> SimpleLineIcons = new()
    {
        "icon-user", "icon-people", "icon-user-female", "icon-user-follow", "icon-user-following", 
        "icon-user-unfollow", "icon-login", "icon-logout", "icon-emotsmile", "icon-phone", 
        "icon-call-end", "icon-call-in", "icon-call-out", "icon-map", "icon-location-pin", 
        "icon-direction", "icon-directions", "icon-compass", "icon-layers", "icon-menu", 
        "icon-list", "icon-options-vertical", "icon-options", "icon-arrow-down", "icon-arrow-left", 
        "icon-arrow-right", "icon-arrow-up", "icon-arrow-up-circle", "icon-arrow-left-circle", 
        "icon-arrow-right-circle", "icon-arrow-down-circle", "icon-check", "icon-clock", 
        "icon-plus", "icon-minus", "icon-close", "icon-event", "icon-exclamation", 
        "icon-organization", "icon-trophy", "icon-screen-smartphone", "icon-screen-desktop", 
        "icon-plane", "icon-notebook", "icon-mustache", "icon-mouse", "icon-magnet", 
        "icon-energy", "icon-disc", "icon-cursor", "icon-cursor-move", "icon-crop", 
        "icon-chemistry", "icon-speedometer", "icon-shield", "icon-screen-tablet", 
        "icon-magic-wand", "icon-hourglass", "icon-graduation", "icon-ghost", 
        "icon-game-controller", "icon-fire", "icon-eyeglass", "icon-envelope-open", 
        "icon-envelope-letter", "icon-bell", "icon-badge", "icon-anchor", "icon-wallet", 
        "icon-vector", "icon-speech", "icon-puzzle", "icon-printer", "icon-present", 
        "icon-playlist", "icon-pin", "icon-picture", "icon-handbag", "icon-globe-alt", 
        "icon-globe", "icon-folder-alt", "icon-folder", "icon-film", "icon-feed", 
        "icon-drop", "icon-drawer", "icon-docs", "icon-doc", "icon-diamond", "icon-cup", 
        "icon-calculator", "icon-bubbles", "icon-briefcase", "icon-book-open", 
        "icon-basket-loaded", "icon-basket", "icon-bag", "icon-action-undo", "icon-action-redo", 
        "icon-wrench", "icon-umbrella", "icon-trash", "icon-tag", "icon-support", "icon-frame", 
        "icon-size-fullscreen", "icon-size-actual", "icon-shuffle", "icon-share-alt", 
        "icon-share", "icon-rocket", "icon-question", "icon-pie-chart", "icon-pencil", 
        "icon-note", "icon-loop", "icon-home", "icon-grid", "icon-graph", "icon-microphone", 
        "icon-music-tone-alt", "icon-music-tone", "icon-earphones-alt", "icon-earphones", 
        "icon-equalizer", "icon-like", "icon-dislike", "icon-control-start", 
        "icon-control-rewind", "icon-control-play", "icon-control-pause", "icon-control-forward", 
        "icon-control-end", "icon-volume-1", "icon-volume-2", "icon-volume-off", "icon-calendar", 
        "icon-bulb", "icon-chart", "icon-ban", "icon-bubble", "icon-camrecorder", "icon-camera", 
        "icon-cloud-download", "icon-cloud-upload", "icon-envelope", "icon-eye", "icon-flag", 
        "icon-heart", "icon-info", "icon-key", "icon-link", "icon-lock", "icon-lock-open", 
        "icon-magnifier", "icon-magnifier-add", "icon-magnifier-remove", "icon-paper-clip", 
        "icon-paper-plane", "icon-power", "icon-refresh", "icon-reload", "icon-settings", 
        "icon-star", "icon-symbol-female", "icon-symbol-male", "icon-target", "icon-credit-card", 
        "icon-paypal", "icon-social-tumblr", "icon-social-twitter", "icon-social-facebook", 
        "icon-social-instagram", "icon-social-linkedin", "icon-social-pinterest", 
        "icon-social-github", "icon-social-google", "icon-social-reddit", "icon-social-skype", 
        "icon-social-dribbble", "icon-social-behance", "icon-social-foursqare", 
        "icon-social-soundcloud", "icon-social-spotify", "icon-social-stumbleupon", 
        "icon-social-youtube", "icon-social-dropbox", "icon-social-vkontakte", "icon-social-steam"
    };

    private static readonly List<string> Ionicons = new()
    {
        "ion-alert", "ion-alert-circled", "ion-android-add", "ion-android-add-circle", 
        "ion-android-alarm-clock", "ion-android-alert", "ion-android-apps", "ion-android-archive", 
        "ion-android-arrow-back", "ion-android-arrow-down", "ion-android-arrow-dropdown", 
        "ion-android-arrow-dropdown-circle", "ion-android-arrow-dropleft", 
        "ion-android-arrow-dropleft-circle", "ion-android-arrow-dropright", 
        "ion-android-arrow-dropright-circle", "ion-android-arrow-dropup", 
        "ion-android-arrow-dropup-circle", "ion-android-arrow-forward", "ion-android-arrow-up", 
        "ion-android-attach", "ion-android-bar", "ion-android-bicycle", "ion-android-boat", 
        "ion-android-bookmark", "ion-android-bulb", "ion-android-bus", "ion-android-calendar", 
        "ion-android-call", "ion-android-camera", "ion-android-cancel", "ion-android-car", 
        "ion-android-cart", "ion-android-chat", "ion-android-checkbox", 
        "ion-android-checkbox-blank", "ion-android-checkbox-outline", 
        "ion-android-checkbox-outline-blank", "ion-android-checkmark-circle", 
        "ion-android-clipboard", "ion-android-close", "ion-android-cloud", 
        "ion-android-cloud-circle", "ion-android-cloud-done", "ion-android-cloud-outline", 
        "ion-android-color-palette", "ion-android-compass", "ion-android-contact", 
        "ion-android-contacts", "ion-android-contract", "ion-android-create", 
        "ion-android-delete", "ion-android-desktop", "ion-android-document", "ion-android-done", 
        "ion-android-done-all", "ion-android-download", "ion-android-drafts", "ion-android-exit", 
        "ion-android-expand", "ion-android-favorite", "ion-android-favorite-outline", 
        "ion-android-film", "ion-android-folder", "ion-android-folder-open", "ion-android-funnel", 
        "ion-android-globe", "ion-android-hand", "ion-android-hangout", "ion-android-happy", 
        "ion-android-home", "ion-android-image", "ion-android-laptop", "ion-android-list", 
        "ion-android-locate", "ion-android-lock", "ion-android-mail", "ion-android-map", 
        "ion-android-menu", "ion-android-microphone", "ion-android-microphone-off", 
        "ion-android-more-horizontal", "ion-android-more-vertical", "ion-android-navigate", 
        "ion-android-notifications", "ion-android-notifications-none", 
        "ion-android-notifications-off", "ion-android-open", "ion-android-options", 
        "ion-android-people", "ion-android-person", "ion-android-person-add", 
        "ion-android-phone-landscape", "ion-android-phone-portrait", "ion-android-pin", 
        "ion-android-plane", "ion-android-playstore", "ion-android-print", 
        "ion-android-radio-button-off", "ion-android-radio-button-on", "ion-android-refresh", 
        "ion-android-remove", "ion-android-remove-circle", "ion-android-restaurant", 
        "ion-android-sad", "ion-android-search", "ion-android-send", "ion-android-settings", 
        "ion-android-share", "ion-android-share-alt", "ion-android-star", 
        "ion-android-star-half", "ion-android-star-outline", "ion-android-stopwatch", 
        "ion-android-subway", "ion-android-sunny", "ion-android-sync", "ion-android-textsms", 
        "ion-android-time", "ion-android-train", "ion-android-unlock", "ion-android-upload", 
        "ion-android-volume-down", "ion-android-volume-mute", "ion-android-volume-off", 
        "ion-android-volume-up", "ion-android-walk", "ion-android-warning", "ion-android-watch", 
        "ion-android-wifi", "ion-bag", "ion-card", "ion-cash", "ion-ios-cart", "ion-social-facebook",
        "ion-social-twitter", "ion-social-instagram", "ion-social-whatsapp"
    };
}
