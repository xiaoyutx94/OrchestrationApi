/**
 * OrchestrationApi — Ops Control Console
 * 共享 App Shell：侧栏导航 / 折叠 / 移动端抽屉 / 登出 / 早期鉴权
 *
 * 用法：
 *   <link rel="stylesheet" href="/css/app.css">
 *   <script src="/js/app-shell.js"></script>
 *   <script>AppShell.init('dashboard');</script>
 *
 * HTML 约定：
 *   <div id="app-sidebar-root"></div>   <!-- 可选，init 时注入侧栏 -->
 *   body 建议加 class="app-body"
 *   主布局外层 class="app-shell"
 */
(function (window, document) {
    'use strict';

    var STORAGE_SIDEBAR = 'appShell.sidebarCollapsed';
    var AUTH_TOKEN_KEY = 'authToken';
    var AUTH_EXPIRES_KEY = 'tokenExpires';
    var AUTH_USERNAME_KEY = 'username';

    /** @type {{ id: string, href: string, label: string, icon: string }[]} */
    var NAV_ITEMS = [
        { id: 'dashboard', href: '/dashboard', label: '控制台', icon: 'dashboard' },
        { id: 'logs', href: '/logs', label: '请求日志', icon: 'logs' },
        { id: 'health-report', href: '/health-report', label: '健康报告', icon: 'health' },
        { id: 'serilog', href: '/serilog', label: '系统日志', icon: 'serilog' }
    ];

    var PAGE_TITLES = {
        dashboard: '控制台',
        logs: '请求日志',
        'health-report': '健康报告',
        serilog: '系统日志'
    };

    // ---------- SVG icons（内联，避免额外依赖） ----------
    var ICONS = {
        dashboard:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="9" rx="1"/><rect x="14" y="3" width="7" height="5" rx="1"/><rect x="14" y="12" width="7" height="9" rx="1"/><rect x="3" y="16" width="7" height="5" rx="1"/></svg>',
        logs:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 6h13"/><path d="M8 12h13"/><path d="M8 18h13"/><path d="M3 6h.01"/><path d="M3 12h.01"/><path d="M3 18h.01"/></svg>',
        health:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>',
        serilog:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/><path d="M8 13h8"/><path d="M8 17h8"/><path d="M8 9h2"/></svg>',
        settings:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>',
        logout:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="M16 17l5-5-5-5"/><path d="M21 12H9"/></svg>',
        menu:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 6h16"/><path d="M4 12h16"/><path d="M4 18h16"/></svg>',
        collapse:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M11 17l-5-5 5-5"/><path d="M18 17l-5-5 5-5"/></svg>',
        expand:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M13 17l5-5-5-5"/><path d="M6 17l5-5-5-5"/></svg>',
        bolt:
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M13 2L3 14h8l-1 8 10-12h-8l1-8z"/></svg>'
    };

    function isMobile() {
        return window.matchMedia && window.matchMedia('(max-width: 768px)').matches;
    }

    function safeGet(key) {
        try {
            return localStorage.getItem(key);
        } catch (e) {
            return null;
        }
    }

    function safeSet(key, value) {
        try {
            localStorage.setItem(key, value);
        } catch (e) {
            /* ignore quota / private mode */
        }
    }

    function safeRemove(key) {
        try {
            localStorage.removeItem(key);
        } catch (e) {
            /* ignore */
        }
    }

    function clearAuthStorage() {
        safeRemove(AUTH_TOKEN_KEY);
        safeRemove(AUTH_EXPIRES_KEY);
        safeRemove(AUTH_USERNAME_KEY);
    }

    function escapeHtml(str) {
        return String(str == null ? '' : str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
    function initialFromName(name) {
        var s = String(name || 'U').trim();
        if (!s) return 'U';
        // 支持中文取首字
        return s.charAt(0).toUpperCase();
    }

    /**
     * 早期鉴权：可在页面 <head> 同步调用，避免闪未授权内容。
     * 校验 localStorage token 格式与本地过期时间。
     * @returns {boolean} true = 通过；false = 已跳转登录
     */
    function earlyAuthCheck() {
        var token = safeGet(AUTH_TOKEN_KEY);
        if (!token) {
            window.location.href = '/login';
            return false;
        }

        try {
            var parts = token.split('.');
            if (parts.length !== 3) {
                clearAuthStorage();
                window.location.href = '/login';
                return false;
            }

            var tokenExpires = safeGet(AUTH_EXPIRES_KEY);
            if (tokenExpires) {
                var expiresAt = new Date(tokenExpires);
                if (!isNaN(expiresAt.getTime()) && new Date() >= expiresAt) {
                    clearAuthStorage();
                    window.location.href = '/login';
                    return false;
                }
            }

            return true;
        } catch (err) {
            clearAuthStorage();
            window.location.href = '/login';
            return false;
        }
    }

    function getUsername() {
        var name = safeGet(AUTH_USERNAME_KEY);
        if (name) return name;

        // 尝试从 JWT payload 解析（不验证签名，仅展示）
        try {
            var token = safeGet(AUTH_TOKEN_KEY);
            if (!token) return 'admin';
            var parts = token.split('.');
            if (parts.length !== 3) return 'admin';
            var payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
            while (payload.length % 4) payload += '=';
            var json = JSON.parse(atob(payload));
            var claim =
                json.unique_name ||
                json.name ||
                json.username ||
                json.sub ||
                json['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'];
            if (claim) {
                safeSet(AUTH_USERNAME_KEY, String(claim));
                return String(claim);
            }
        } catch (e) {
            /* ignore parse errors */
        }
        return 'admin';
    }

    function buildNavHtml(pageId) {
        var html = '';
        for (var i = 0; i < NAV_ITEMS.length; i++) {
            var item = NAV_ITEMS[i];
            var active = item.id === pageId ? ' is-active' : '';
            var icon = ICONS[item.icon] || ICONS.dashboard;
            html +=
                '<a class="nav-item' +
                active +
                '" href="' +
                item.href +
                '" data-page="' +
                item.id +
                '" title="' +
                escapeHtml(item.label) +
                '">' +
                '<span class="nav-item-icon">' +
                icon +
                '</span>' +
                '<span class="nav-item-label">' +
                escapeHtml(item.label) +
                '</span>' +
                '</a>';
        }
        return html;
    }

    /**
     * 渲染侧栏 HTML 到指定元素
     * @param {HTMLElement} el
     * @param {string} [pageId]
     */
    function renderSidebar(el, pageId) {
        if (!el) return;

        var current = pageId || AppShell.currentPage || '';
        var username = getUsername();
        var initial = escapeHtml(initialFromName(username));
        var safeName = escapeHtml(username);

        // 遮罩放在侧栏前面：同级时侧栏在后，再叠加更高 z-index，避免菜单被挡住
        el.innerHTML =
            '<div id="app-sidebar-overlay" class="app-sidebar-overlay" hidden></div>' +
            '<aside id="app-sidebar" class="app-sidebar" aria-label="主导航">' +
            '  <div class="sidebar-brand">' +
            '    <div class="sidebar-brand-mark" aria-hidden="true">' +
            ICONS.bolt +
            '    </div>' +
            '    <div class="sidebar-brand-text">' +
            '      <div class="sidebar-brand-title">OrchestrationApi</div>' +
            '      <div class="sidebar-brand-sub">Ops Control Console</div>' +
            '    </div>' +
            '  </div>' +
            '  <nav class="sidebar-nav" aria-label="页面导航">' +
            '    <div class="nav-section">' +
            '      <div class="nav-section-label">监控</div>' +
            buildNavHtml(current) +
            '    </div>' +
            '  </nav>' +
            '  <div class="sidebar-footer">' +
            '    <div class="sidebar-user" title="' +
            safeName +
            '">' +
            '      <div class="sidebar-user-avatar">' +
            initial +
            '</div>' +
            '      <div class="sidebar-footer-meta">' +
            '        <div class="sidebar-user-name" data-app-username>' +
            safeName +
            '</div>' +
            '        <div class="sidebar-user-role">管理员</div>' +
            '      </div>' +
            '    </div>' +
            '    <button type="button" class="nav-item" data-app-action="settings" title="账户设置">' +
            '      <span class="nav-item-icon">' +
            ICONS.settings +
            '</span>' +
            '      <span class="nav-item-label">账户设置</span>' +
            '    </button>' +
            '    <button type="button" class="nav-item" data-app-action="logout" title="登出">' +
            '      <span class="nav-item-icon">' +
            ICONS.logout +
            '</span>' +
            '      <span class="nav-item-label">登出</span>' +
            '    </button>' +
            '  </div>' +
            '</aside>';
    }

    function ensureToastHost() {
        var host = document.getElementById('app-toast-host');
        if (!host) {
            host = document.createElement('div');
            host.id = 'app-toast-host';
            host.setAttribute('aria-live', 'polite');
            host.setAttribute('aria-relevant', 'additions');
            document.body.appendChild(host);
        }
        return host;
    }

    /**
     * 简易 toast（可选工具）
     * @param {string} message
     * @param {{ type?: string, title?: string, duration?: number }} [opts]
     */
    function toast(message, opts) {
        opts = opts || {};
        var type = opts.type || 'info';
        var duration = typeof opts.duration === 'number' ? opts.duration : 3200;
        var host = ensureToastHost();
        var el = document.createElement('div');
        el.className = 'app-toast is-' + type;
        el.setAttribute('role', 'status');

        var titleHtml = opts.title
            ? '<div class="app-toast-title">' + escapeHtml(opts.title) + '</div>'
            : '';

        el.innerHTML =
            '<div class="app-toast-body">' +
            titleHtml +
            '<div>' +
            escapeHtml(message) +
            '</div></div>' +
            '<button type="button" class="app-toast-close" aria-label="关闭">&times;</button>';

        function remove() {
            if (el._removed) return;
            el._removed = true;
            el.classList.add('is-leaving');
            setTimeout(function () {
                if (el.parentNode) el.parentNode.removeChild(el);
            }, 200);
        }

        el.querySelector('.app-toast-close').addEventListener('click', remove);
        host.appendChild(el);
        if (duration > 0) {
            setTimeout(remove, duration);
        }
        return el;
    }

    function getSidebarEl() {
        return document.getElementById('app-sidebar');
    }

    function getOverlayEl() {
        return document.getElementById('app-sidebar-overlay');
    }

    function applyCollapsedState(collapsed) {
        var sidebar = getSidebarEl();
        var shell = document.querySelector('.app-shell');
        var body = document.body;

        if (sidebar) {
            if (collapsed && !isMobile()) {
                sidebar.classList.add('is-collapsed');
            } else if (!isMobile()) {
                sidebar.classList.remove('is-collapsed');
            }
        }

        if (shell) {
            shell.classList.toggle('sidebar-collapsed', !!collapsed && !isMobile());
        }
        if (body) {
            body.classList.toggle('sidebar-collapsed', !!collapsed && !isMobile());
        }

        // 更新顶栏折叠按钮图标（若存在）
        var toggleBtns = document.querySelectorAll('[data-app-action="toggle-sidebar"]');
        for (var i = 0; i < toggleBtns.length; i++) {
            var btn = toggleBtns[i];
            var iconSlot = btn.querySelector('[data-toggle-icon]') || btn;
            if (isMobile()) {
                if (btn.querySelector('[data-toggle-icon]')) {
                    btn.querySelector('[data-toggle-icon]').innerHTML = ICONS.menu;
                }
                btn.setAttribute('aria-label', '打开导航');
                btn.setAttribute('title', '打开导航');
            } else {
                if (btn.querySelector('[data-toggle-icon]')) {
                    btn.querySelector('[data-toggle-icon]').innerHTML = collapsed
                        ? ICONS.expand
                        : ICONS.collapse;
                }
                btn.setAttribute('aria-label', collapsed ? '展开侧栏' : '折叠侧栏');
                btn.setAttribute('title', collapsed ? '展开侧栏' : '折叠侧栏');
            }
            // 若按钮本身就是图标容器
            if (!btn.querySelector('[data-toggle-icon]') && btn.classList.contains('sidebar-toggle')) {
                // 保留已有内容；仅在空时填充
                if (!btn.innerHTML.trim()) {
                    btn.innerHTML = isMobile()
                        ? ICONS.menu
                        : collapsed
                          ? ICONS.expand
                          : ICONS.collapse;
                }
            }
        }
    }

    function setMobileOpen(open) {
        var sidebar = getSidebarEl();
        var overlay = getOverlayEl();

        if (sidebar) {
            sidebar.classList.toggle('is-mobile-open', !!open);
        }
        if (overlay) {
            if (open) {
                overlay.hidden = false;
                // 强制 reflow 以便 transition
                void overlay.offsetWidth;
                overlay.classList.add('is-open');
            } else {
                overlay.classList.remove('is-open');
                setTimeout(function () {
                    if (overlay && !overlay.classList.contains('is-open')) {
                        overlay.hidden = true;
                    }
                }, 260);
            }
        }

        if (document.body) {
            document.body.style.overflow = open && isMobile() ? 'hidden' : '';
        }
    }

    function toggleSidebar() {
        if (isMobile()) {
            var sidebar = getSidebarEl();
            var open = !(sidebar && sidebar.classList.contains('is-mobile-open'));
            setMobileOpen(open);
            return;
        }

        var collapsed = safeGet(STORAGE_SIDEBAR) === '1';
        collapsed = !collapsed;
        safeSet(STORAGE_SIDEBAR, collapsed ? '1' : '0');
        applyCollapsedState(collapsed);
    }

    function closeMobileSidebar() {
        setMobileOpen(false);
    }

    function logout() {
        var token = safeGet(AUTH_TOKEN_KEY);
        clearAuthStorage();

        function goLogin() {
            window.location.href = '/login';
        }

        if (!token) {
            goLogin();
            return;
        }

        try {
            fetch('/auth/logout', {
                method: 'POST',
                headers: {
                    Authorization: 'Bearer ' + token,
                    'Content-Type': 'application/json'
                }
            })
                .then(function () {
                    goLogin();
                })
                .catch(function (err) {
                    console.error('Logout error:', err);
                    goLogin();
                });
        } catch (e) {
            goLogin();
        }
    }

    function onDocumentClick(e) {
        var target = e.target;
        if (!target || !target.closest) return;

        var actionEl = target.closest('[data-app-action]');
        if (actionEl) {
            var action = actionEl.getAttribute('data-app-action');
            if (action === 'toggle-sidebar') {
                e.preventDefault();
                toggleSidebar();
                return;
            }
            if (action === 'logout') {
                e.preventDefault();
                logout();
                return;
            }
            if (action === 'settings') {
                e.preventDefault();
                // 非控制台页没有设置弹窗：跳转到控制台打开
                var page = AppShell.currentPage || '';
                if (page && page !== 'dashboard') {
                    window.location.href = '/dashboard?settings=1';
                    return;
                }
                try {
                    document.dispatchEvent(
                        new CustomEvent('app:open-settings', {
                            bubbles: true,
                            detail: { source: 'app-shell' }
                        })
                    );
                } catch (err) {
                    var ev = document.createEvent('CustomEvent');
                    ev.initCustomEvent('app:open-settings', true, true, { source: 'app-shell' });
                    document.dispatchEvent(ev);
                }
                return;
            }
            if (action === 'close-sidebar') {
                e.preventDefault();
                closeMobileSidebar();
                return;
            }
        }

        // 点击遮罩关闭
        if (target.id === 'app-sidebar-overlay' || target.classList.contains('app-sidebar-overlay')) {
            closeMobileSidebar();
            return;
        }

        // 点击侧栏导航链接：先关抽屉，再正常跳转
        var navLink = target.closest('.app-sidebar a.nav-item[href]');
        if (navLink && isMobile()) {
            closeMobileSidebar();
            // 不 preventDefault，让浏览器继续导航
        }
    }

    function onKeydown(e) {
        if (e.key === 'Escape') {
            closeMobileSidebar();
        }
    }

    function onResize() {
        // 切到桌面时关闭移动抽屉
        if (!isMobile()) {
            closeMobileSidebar();
            var collapsed = safeGet(STORAGE_SIDEBAR) === '1';
            applyCollapsedState(collapsed);
        } else {
            var sidebar = getSidebarEl();
            if (sidebar) sidebar.classList.remove('is-collapsed');
            var shell = document.querySelector('.app-shell');
            if (shell) shell.classList.remove('sidebar-collapsed');
            document.body.classList.remove('sidebar-collapsed');
        }
    }

    /**
     * 确保顶栏有折叠按钮（若页面已自建 topbar 且带 data-app-action 则跳过）
     */
    function ensureTopbarToggle() {
        var existing = document.querySelector('[data-app-action="toggle-sidebar"]');
        if (existing) return;

        var topbar = document.querySelector('.app-topbar');
        if (!topbar) return;

        var left = topbar.querySelector('.app-topbar-left');
        if (!left) {
            left = document.createElement('div');
            left.className = 'app-topbar-left';
            topbar.insertBefore(left, topbar.firstChild);
        }

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'sidebar-toggle';
        btn.setAttribute('data-app-action', 'toggle-sidebar');
        btn.setAttribute('aria-label', '切换侧栏');
        btn.innerHTML = '<span data-toggle-icon">' + ICONS.menu + '</span>';
        left.insertBefore(btn, left.firstChild);
    }

    /**
     * 同步侧栏用户名展示
     * @param {string} name
     */
    function setUsername(name) {
        if (name) safeSet(AUTH_USERNAME_KEY, String(name));
        var nodes = document.querySelectorAll('[data-app-username]');
        var display = getUsername();
        for (var i = 0; i < nodes.length; i++) {
            nodes[i].textContent = display;
        }
        var avatars = document.querySelectorAll('.sidebar-user-avatar');
        for (var j = 0; j < avatars.length; j++) {
            avatars[j].textContent = initialFromName(display);
        }
    }

    /**
     * 标记壳层就绪，避免菜单切换时 FOUC / 布局跳动闪烁
     */
    function markReady() {
        try {
            document.documentElement.classList.remove('app-pending');
            document.documentElement.classList.add('app-ready');
            if (document.body) {
                document.body.classList.add('app-ready');
            }
        } catch (e) {
            /* ignore */
        }
    }

    function scheduleReady() {
        // 有 Alpine 时等其完成首轮绑定，避免 x-cloak 区域先空后显
        var done = false;
        function finish() {
            if (done) return;
            done = true;
            markReady();
        }

        if (window.Alpine) {
            // Alpine 可能已初始化
            if (typeof queueMicrotask === 'function') {
                queueMicrotask(finish);
            } else {
                setTimeout(finish, 0);
            }
        } else {
            document.addEventListener('alpine:initialized', finish, { once: true });
        }

        // 兜底：无 Alpine 或事件丢失时也要显示
        setTimeout(finish, 150);
    }

    /**
     * 初始化 App Shell
     * @param {string} pageId dashboard|logs|serilog|health-report
     * @param {{ skipAuth?: boolean, username?: string }} [options]
     */
    function init(pageId, options) {
        options = options || {};
        AppShell.currentPage = pageId || '';

        if (!options.skipAuth) {
            // 非阻塞：仅在无 token 时跳转；完整校验可由页面业务逻辑完成
            var token = safeGet(AUTH_TOKEN_KEY);
            if (!token) {
                window.location.href = '/login';
                return AppShell;
            }
        }

        if (options.username) {
            safeSet(AUTH_USERNAME_KEY, String(options.username));
        }

        // body 基础 class
        if (document.body && !document.body.classList.contains('app-body')) {
            document.body.classList.add('app-body');
        }

        // 注入侧栏
        var root = document.getElementById('app-sidebar-root');
        if (root) {
            renderSidebar(root, AppShell.currentPage);
        } else if (!document.getElementById('app-sidebar')) {
            // 无 root 时在 body 前部创建容器
            var autoRoot = document.createElement('div');
            autoRoot.id = 'app-sidebar-root';
            if (document.body.firstChild) {
                document.body.insertBefore(autoRoot, document.body.firstChild);
            } else {
                document.body.appendChild(autoRoot);
            }
            renderSidebar(autoRoot, AppShell.currentPage);
        }

        ensureToastHost();
        ensureTopbarToggle();

        // 桌面折叠状态
        var collapsed = safeGet(STORAGE_SIDEBAR) === '1';
        applyCollapsedState(collapsed);

        // 事件（避免重复绑定）
        if (!AppShell._bound) {
            document.addEventListener('click', onDocumentClick);
            document.addEventListener('keydown', onKeydown);
            window.addEventListener('resize', onResize);
            AppShell._bound = true;
        }

        // 顶栏标题同步（若存在空标题节点）
        var titleEl = document.querySelector('[data-app-page-title]');
        if (titleEl && !titleEl.textContent.trim()) {
            titleEl.textContent = PAGE_TITLES[AppShell.currentPage] || '';
        }

        // 壳层已注入：安排“就绪”显示，消除切换闪烁
        scheduleReady();

        return AppShell;
    }

    /** @type {any} */
    var AppShell = {
        currentPage: '',
        init: init,
        toggleSidebar: toggleSidebar,
        closeMobileSidebar: closeMobileSidebar,
        logout: logout,
        getUsername: getUsername,
        setUsername: setUsername,
        earlyAuthCheck: earlyAuthCheck,
        renderSidebar: renderSidebar,
        toast: toast,
        markReady: markReady,
        navItems: NAV_ITEMS,
        icons: ICONS,
        _bound: false
    };

    // 兼容旧页面全局 logout()
    if (typeof window.logout !== 'function') {
        window.logout = function () {
            AppShell.logout();
        };
    }

    window.AppShell = AppShell;
})(window, document);
