(function () {
    const storageKey = "adr-portal-theme";
    const darkTheme = "dark";
    const lightTheme = "light";

    const isTheme = (value) => value === darkTheme || value === lightTheme;

    const getStoredPreference = () => {
        const storedTheme = localStorage.getItem(storageKey);
        return isTheme(storedTheme) ? storedTheme : null;
    };

    const getSystemPreference = () => window.matchMedia("(prefers-color-scheme: dark)").matches
        ? darkTheme
        : lightTheme;

    const applyTheme = (theme, persist = false) => {
        const normalizedTheme = isTheme(theme) ? theme : lightTheme;
        document.documentElement.setAttribute("data-theme", normalizedTheme);
        document.documentElement.style.colorScheme = normalizedTheme;

        if (persist) {
            localStorage.setItem(storageKey, normalizedTheme);
        }

        return normalizedTheme;
    };

    const resolveInitialTheme = () => {
        const storedTheme = getStoredPreference();
        if (storedTheme !== null) {
            return applyTheme(storedTheme);
        }

        return applyTheme(getSystemPreference());
    };

    const toggleTheme = (currentTheme) => {
        const nextTheme = currentTheme === darkTheme ? lightTheme : darkTheme;
        return applyTheme(nextTheme, true);
    };

    window.adrTheme = {
        applyTheme,
        resolveInitialTheme,
        toggleTheme
    };

    resolveInitialTheme();
})();
