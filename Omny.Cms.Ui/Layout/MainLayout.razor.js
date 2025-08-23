export function omnyLogout() {
    // Clear local storage
    localStorage.clear();
    // Clear all cookies
    document.cookie.split(';').forEach(function(c) {
        document.cookie = c.replace(/^ +/, '').replace(/=.*/, '=;expires=' + new Date(0).toUTCString() + ';path=/');
    });
}

export function setTopMenuVisible(show) {
    const topRow = document.querySelector('.top-row');
    if (topRow) {
        topRow.style.display = show ? '' : 'none';
    }
}
