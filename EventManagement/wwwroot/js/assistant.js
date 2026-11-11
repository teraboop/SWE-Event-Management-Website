// AI features on the page:
// 1. The chat assistant widget (#11). The conversation is kept in
//    sessionStorage so it survives moving between pages, and sent with each
//    question so follow-ups make sense. Replies are inserted as text (never
//    HTML), and links only come from the server.
// 2. Sections with data-load-url (the "Recommended for you" section, #12)
//    are filled in after the page loads, so slow AI calls don't hold it up.
(function () {
    "use strict";

    // ----- Deferred sections ------------------------------------------------
    document.querySelectorAll("[data-load-url]").forEach(function (section) {
        fetch(section.dataset.loadUrl, { headers: { "X-Requested-With": "fetch" } })
            .then(function (response) {
                if (!response.ok) { throw new Error(response.status); }
                return response.text();
            })
            .then(function (html) { section.innerHTML = html; })
            .catch(function () {
                section.innerHTML = '<p class="text-muted">Recommendations aren\'t available right now.</p>';
            });
    });

    // ----- Chat assistant ---------------------------------------------------
    var root = document.getElementById("assistant");
    if (!root) { return; }

    var storageKey = "nostalgia-assistant-history";
    var maxStoredTurns = 20;
    var toggle = document.getElementById("assistant-toggle");
    var panel = document.getElementById("assistant-panel");
    var messages = document.getElementById("assistant-messages");
    var suggestions = document.getElementById("assistant-suggestions");
    var form = document.getElementById("assistant-form");
    var input = document.getElementById("assistant-input");
    var sendButton = document.getElementById("assistant-send");
    var token = root.querySelector('input[name="__RequestVerificationToken"]').value;

    function loadHistory() {
        try { return JSON.parse(sessionStorage.getItem(storageKey)) || []; }
        catch (e) { return []; }
    }

    function saveHistory(history) {
        try { sessionStorage.setItem(storageKey, JSON.stringify(history.slice(-maxStoredTurns))); }
        catch (e) { /* storage unavailable: the chat still works for this page */ }
    }

    var history = loadHistory();

    function addMessage(role, text, links) {
        var bubble = document.createElement("div");
        bubble.className = "assistant-message " + (role === "user" ? "assistant-message-user" : "assistant-message-bot");
        bubble.textContent = text;

        if (links && links.length) {
            var list = document.createElement("div");
            list.className = "assistant-links";
            links.forEach(function (link) {
                var anchor = document.createElement("a");
                anchor.className = "assistant-link";
                anchor.href = link.url;
                var title = document.createElement("span");
                title.className = "fw-semibold";
                title.textContent = link.title;
                var subtitle = document.createElement("span");
                subtitle.className = "small text-muted";
                subtitle.textContent = link.subtitle;
                anchor.append(title, subtitle);
                list.append(anchor);
            });
            bubble.append(list);
        }

        messages.append(bubble);
        messages.scrollTop = messages.scrollHeight;
        return bubble;
    }

    function restore() {
        if (history.length && suggestions) { suggestions.hidden = true; }
        history.forEach(function (turn) { addMessage(turn.role, turn.text, turn.links); });
    }

    function setOpen(open) {
        panel.hidden = !open;
        toggle.setAttribute("aria-expanded", String(open));
        toggle.hidden = open;
        if (open) { input.focus(); }
        try { sessionStorage.setItem(storageKey + "-open", open ? "1" : "0"); } catch (e) { }
    }

    function ask(question) {
        if (suggestions) { suggestions.hidden = true; }
        addMessage("user", question);
        var thinking = addMessage("assistant", "Thinking...");
        thinking.classList.add("assistant-message-thinking");
        input.value = "";
        input.disabled = sendButton.disabled = true;

        var previous = history.map(function (turn) { return { role: turn.role, text: turn.text }; });

        fetch("/assistant/ask", {
            method: "POST",
            headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
            body: JSON.stringify({ message: question, history: previous })
        })
            .then(function (response) {
                if (!response.ok) { throw new Error(response.status); }
                return response.json();
            })
            .then(function (reply) {
                thinking.remove();
                addMessage("assistant", reply.answer, reply.links);
                history.push({ role: "user", text: question });
                history.push({ role: "assistant", text: reply.answer, links: reply.links });
                saveHistory(history);
            })
            .catch(function () {
                thinking.remove();
                addMessage("assistant", "Sorry, something went wrong. Please try again.");
            })
            .finally(function () {
                input.disabled = sendButton.disabled = false;
                input.focus();
            });
    }

    toggle.addEventListener("click", function () { setOpen(true); });
    document.getElementById("assistant-close").addEventListener("click", function () { setOpen(false); });
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && !panel.hidden) { setOpen(false); toggle.focus(); }
    });

    document.getElementById("assistant-clear").addEventListener("click", function () {
        history = [];
        saveHistory(history);
        messages.querySelectorAll(".assistant-message:not(:first-child)").forEach(function (node) { node.remove(); });
        if (suggestions) { suggestions.hidden = false; }
        input.focus();
    });

    if (suggestions) {
        suggestions.addEventListener("click", function (event) {
            var button = event.target.closest(".assistant-suggestion");
            if (button) { ask(button.textContent.trim()); }
        });
    }

    form.addEventListener("submit", function (event) {
        event.preventDefault();
        var question = input.value.trim();
        if (question) { ask(question); }
    });

    restore();
    var wasOpen = false;
    try { wasOpen = sessionStorage.getItem(storageKey + "-open") === "1"; } catch (e) { }
    if (wasOpen) { setOpen(true); }
})();
