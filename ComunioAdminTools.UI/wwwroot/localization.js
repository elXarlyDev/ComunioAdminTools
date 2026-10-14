const preferenceKey = "comunio.language";
const catalogs = new Map();
const spanish = "es-ES";
const valencian = "ca-ES-valencia";
let currentLanguage = spanish;
let initialization;

export function normalizeCulture(culture) {
    return /^ca(?:-|$)/i.test(culture || "") ? valencian : spanish;
}

async function catalog(language) {
    if (!catalogs.has(language)) {
        const response = await fetch(new URL(`localization/${language}.xml`, import.meta.url));
        if (!response.ok) throw new Error(`Localization HTTP ${response.status}`);
        const xml = new DOMParser().parseFromString(await response.text(), "application/xml");
        if (xml.querySelector("parsererror")) throw new Error("Invalid localization resource");
        catalogs.set(language, Object.fromEntries(Array.from(xml.querySelectorAll("data"))
            .map(entry => [entry.getAttribute("name"), entry.querySelector("value").textContent])));
    }
    return catalogs.get(language);
}

async function updateDocument(language) {
    const strings = await catalog(language);
    if (language !== currentLanguage) return;
    document.documentElement.lang = language;
    document.title = strings.AppTitle;
    for (const element of document.querySelectorAll("[data-localization]")) {
        element.textContent = strings[element.dataset.localization] || "";
    }
    for (const element of document.querySelectorAll("[data-localization-label]")) {
        element.setAttribute("aria-label", strings[element.dataset.localizationLabel] || "");
    }
    document.documentElement.style.setProperty("--app-error-message", JSON.stringify(strings.UnhandledError));
}

async function loadInitialLanguage() {
    let stored;
    try { stored = localStorage.getItem(preferenceKey); } catch { /* Storage is optional. */ }
    currentLanguage = normalizeCulture(stored || navigator.languages?.[0] || navigator.language);
    document.documentElement.lang = currentLanguage;
    try { await updateDocument(currentLanguage); }
    catch {
        currentLanguage = spanish;
        document.documentElement.lang = spanish;
        try { await updateDocument(spanish); } catch { /* .NET has both embedded resources. */ }
    }
    return currentLanguage;
}

export function initialize() {
    return initialization ??= loadInitialLanguage();
}

export async function setLanguage(culture) {
    currentLanguage = normalizeCulture(culture);
    document.documentElement.lang = currentLanguage;
    try { localStorage.setItem(preferenceKey, currentLanguage); } catch { /* Storage is optional. */ }
    try { await updateDocument(currentLanguage); } catch { /* Razor remains localized. */ }
}

// Also translate loading and fatal startup errors before .NET is ready.
void initialize();
