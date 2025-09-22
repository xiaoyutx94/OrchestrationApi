// 自定义确认框函数
function showConfirm(message, title = '确认操作') {
    return new Promise((resolve) => {
        const modal = document.getElementById('confirmModal');
        const messageEl = document.getElementById('confirmMessage');
        const titleEl = modal.querySelector('h3');
        const yesBtn = document.getElementById('confirmYes');
        const noBtn = document.getElementById('confirmNo');

        messageEl.textContent = message;
        titleEl.textContent = title;
        modal.classList.remove('hidden');

        // 添加淡入动画
        setTimeout(() => {
            modal.querySelector('.relative').style.transform = 'scale(1)';
            modal.querySelector('.relative').style.opacity = '1';
        }, 10);

        const handleYes = () => {
            modal.classList.add('hidden');
            yesBtn.removeEventListener('click', handleYes);
            noBtn.removeEventListener('click', handleNo);
            document.removeEventListener('keydown', handleKeydown);
            resolve(true);
        };

        const handleNo = () => {
            modal.classList.add('hidden');
            yesBtn.removeEventListener('click', handleYes);
            noBtn.removeEventListener('click', handleNo);
            document.removeEventListener('keydown', handleKeydown);
            resolve(false);
        };

        const handleKeydown = (e) => {
            if (e.key === 'Enter') {
                handleYes();
            } else if (e.key === 'Escape') {
                handleNo();
            }
        };

        yesBtn.addEventListener('click', handleYes);
        noBtn.addEventListener('click', handleNo);
        document.addEventListener('keydown', handleKeydown);

        // 点击遮罩关闭
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                handleNo();
            }
        });
    });
}

// 自定义提示框函数
function showAlert(message, type = 'info', title = '提示') {
    return new Promise((resolve) => {
        const modal = document.getElementById('alertModal');
        const messageEl = document.getElementById('alertMessage');
        const titleEl = document.getElementById('alertTitle');
        const iconEl = document.getElementById('alertIcon');
        const okBtn = document.getElementById('alertOk');

        messageEl.textContent = message;
        titleEl.textContent = title;

        // 根据类型设置图标和颜色
        let iconHTML = '';
        let buttonClass = 'bg-blue-500 hover:bg-blue-600 focus:ring-blue-300';

        switch (type) {
            case 'success':
                iconHTML = `
                            <div class="bg-green-100 rounded-full p-3">
                                <svg class="h-6 w-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                                </svg>
                            </div>`;
                buttonClass = 'bg-green-500 hover:bg-green-600 focus:ring-green-300';
                break;
            case 'error':
                iconHTML = `
                            <div class="bg-red-100 rounded-full p-3">
                                <svg class="h-6 w-6 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                                </svg>
                            </div>`;
                buttonClass = 'bg-red-500 hover:bg-red-600 focus:ring-red-300';
                break;
            case 'warning':
                iconHTML = `
                            <div class="bg-yellow-100 rounded-full p-3">
                                <svg class="h-6 w-6 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16c-.77.833.192 2.5 1.732 2.5z"></path>
                                </svg>
                            </div>`;
                buttonClass = 'bg-yellow-500 hover:bg-yellow-600 focus:ring-yellow-300';
                break;
            default: // info
                iconHTML = `
                            <div class="bg-blue-100 rounded-full p-3">
                                <svg class="h-6 w-6 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                                </svg>
                            </div>`;
        }

        iconEl.innerHTML = iconHTML;
        okBtn.className = `px-4 py-2 text-white text-base font-medium rounded-md w-20 focus:outline-none transition-colors ${buttonClass}`;

        modal.classList.remove('hidden');

        // 添加淡入动画
        setTimeout(() => {
            modal.querySelector('.relative').style.transform = 'scale(1)';
            modal.querySelector('.relative').style.opacity = '1';
        }, 10);

        const handleOk = () => {
            modal.classList.add('hidden');
            okBtn.removeEventListener('click', handleOk);
            document.removeEventListener('keydown', handleKeydown);
            resolve();
        };

        const handleKeydown = (e) => {
            if (e.key === 'Enter' || e.key === 'Escape') {
                handleOk();
            }
        };

        okBtn.addEventListener('click', handleOk);
        document.addEventListener('keydown', handleKeydown);

        // 点击遮罩关闭
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                handleOk();
            }
        });
    });
}

function multiProviderDashboard() {
    return {
        systemHealth: {
            status: "healthy",
            total_groups: 0,
            healthy_groups: 0,
            total_keys: 0,
            active_keys: 0,
            uptime: 0,
        },
        versionInfo: null,
        checkingVersion: false,
        providerStatuses: {},
        providerModels: {},
        selectedProvider: "",
        loadingModels: false,
        lastUpdate: new Date(),

        // 用户信息管理相关
        user: {
            username: '',
            role: ''
        },
        showUserSettingsModal: false,
        updatingUserInfo: false,
        userForm: {
            currentPassword: '',
            newUsername: '',
            newPassword: '',
            confirmPassword: ''
        },

        // 分组导出相关
        selectedGroups: [],

        // 分组导入相关
        showImportModal: false,
        selectedFile: null,
        importing: false,

        // 密钥状态相关
        keyStatus: {
            total_keys: 0,
            total_valid: 0,
            total_invalid: 0,
            last_updated: null,
            groups: {},
        },

        // 代理密钥管理相关
        showProxyKeyModal: false,
        showGenerateProxyKeyForm: false,
        showEditProxyKeyModal: false,
        proxyKeys: [],
        newProxyKey: {
            name: "",
            description: "",
            allowed_groups: [],
            group_selection_config: {
                strategy: "round_robin",
                group_weights: [],
            },
        },
        editingProxyKey: {
            id: "",
            name: "",
            description: "",
            is_active: true,
            allowed_groups: [],
            group_selection_config: {
                strategy: "round_robin",
                group_weights: [],
            },
        },
        // 策略权重快照（切换策略时恢复此前填写的值）
        newProxyKeyWeightsSnapshot: {},
        editingProxyKeyWeightsSnapshot: {},
        _newProxyKeyPrevStrategy: "round_robin",
        _editingProxyKeyPrevStrategy: "round_robin",
        // 代理密钥分页、搜索和排序
        proxyKeySearch: "",
        proxyKeySortBy: "created_time_desc", // 默认按创建时间倒序
        proxyKeyPage: 1,
        proxyKeyPageSize: 10,
        proxyKeyPagination: {
            page: 1,
            page_size: 10,
            total: 0,
            total_pages: 0,
            has_prev: false,
            has_next: false,
        },
        loadingKeyStatus: false,
        loadingValidation: false,
        loadingSystemHealth: false,
        loadingProviderStatuses: false,
        loadingHealthRefresh: false,
        keyStatusTimer: null,

        // 密钥使用统计相关
        showKeyUsageStatsModal: false,
        currentGroupUsageStats: null,
        loadingUsageStats: false,
        selectedForceUpdateKey: null,
        showForceUpdateModal: false,
        forceUpdateStatus: 'valid',

        // 服务商筛选和分页
        providerSearchQuery: "",
        providerStatusFilter: "",
        providerTypeFilter: "",
        providerEnabledFilter: "",
        filteredProviders: [],
        providerPage: 1,
        providersPerPage: 5,
        validatingAllKeys: false,
        validatingGroups: {}, // 跟踪每个分组的验证状态

        // 分组管理相关
        showCreateGroupModal: false,
        showEditGroupModal: false,
        showBatchAddModal: false,
        submittingGroup: false,
        editingGroupId: "",
        message: "",
        messageType: "success",

        // 密钥分页相关
        selectedKeys: [],
        keyPage: 1,
        keysPerPage: 5,
        batchKeysText: "",

        // 密钥验证相关
        validatingKeys: false,
        keyValidationStatus: {}, // 存储每个密钥的验证状态
        invalidKeyIndexes: [], // 存储无效密钥的索引
        forcingKeyStatus: {}, // 存储正在强制设置状态的密钥索引
        bulkDeletingInvalidKeys: false, // 一键删除失效密钥的状态
        clearingInvalidKeys: false, // 清除无效密钥的状态
        clearingEmptyGroups: false, // 清除空白分组的状态

        // 模型相关
        availableModels: [],
        filteredModels: [],
        loadingModels: false,
        modelSearchQuery: "",

        groupFormData: {
            group_id: "",
            name: "",
            provider_type: "",
            base_url: "",
            enabled: true,
            timeout: 30,
            max_retries: 3,
            rotation_strategy: "round_robin",
            api_keys: [""],
            models: [],
            use_native_response: false,
            rpm_limit: 0,
            request_params: {},
            model_mappings: {},
            headers: {},
            priority: 0,
            fake_streaming: false, // 假流模式配置
            proxy_enabled: false,
            proxy_config: {
                type: "http",
                host: "",
                port: 8080,
                username: "",
                password: "",
                bypass_local: true,
                bypass_domains: []
            },
            proxy_bypass_domains_text: "",
        },
        modelsText: "",

        // JSON请求参数相关
        requestParamsText: "",
        showRequestParamsHelp: false,
        requestParamsValidationMessage: "",
        requestParamsValidationError: false,
        // JSON请求头相关
        headersText: "",
        showHeadersHelp: false,
        headersValidationMessage: "",
        headersValidationError: false,

        // 模型重命名相关
        modelMappings: [],
        showModelMappingHelp: false,
        allConfiguredAliases: [], // 存储所有服务商配置的模型别名

        // 粘贴模型映射相关
        showPasteModelMappingModal: false,
        pasteModelMappingText: '',
        pasteModelMappingValidationMessage: '',
        pasteModelMappingValidationError: false,

        // 计算属性
        get providerPageOffset() {
            return (this.providerPage - 1) * this.providersPerPage;
        },

        get paginatedProviders() {
            const start = this.providerPageOffset;
            const end = start + this.providersPerPage;
            const providersArray = Object.entries(
                this.filteredProviders,
            );
            return providersArray
                .slice(start, end)
                .reduce((acc, [groupId, provider]) => {
                    acc[groupId] = provider;
                    return acc;
                }, {});
        },

        get totalProviderPages() {
            return Math.ceil(
                Object.keys(this.filteredProviders).length /
                this.providersPerPage,
            );
        },

        // 分组管理计算属性
        get keyPageOffset() {
            return (this.keyPage - 1) * this.keysPerPage;
        },

        get paginatedKeys() {
            const start = this.keyPageOffset;
            const end = start + this.keysPerPage;
            return this.groupFormData.api_keys.slice(start, end);
        },

        get totalKeyPages() {
            return Math.ceil(
                this.groupFormData.api_keys.length /
                this.keysPerPage,
            );
        },

        // 系统状态计算属性（已移除平均响应时间计算）

        async init() {
            // 首先检查是否已登录
            if (!await this.checkAuthentication()) {
                window.location.href = '/login';
                return;
            }

            // 初始化验证状态对象
            this.validatingGroups = {};

            // 系统健康状态和服务商分组状态需要默认展示
            await this.loadSystemHealth();
            await this.loadProviderStatuses();
            await this.loadProxyKeys();
            await this.loadAllConfiguredAliases();

            // 检查版本更新（仅在生产环境）
            await this.checkVersion();

            // 加载密钥状态数据，确保首次启动时能正确显示密钥数量
            await this.loadKeyStatus();

            // 加载已保存的验证状态（初始化时不显示消息）
            await this.loadPersistedValidationStatus(false);
            this.filterProviders();

            // 启动系统健康状态的定时刷新（只刷新第一排的系统信息）
            this.startSystemHealthRefresh();

            // 添加页面可见性变化监听器，确保用户回到页面时数据是最新的
            document.addEventListener("visibilitychange", () => {
                if (!document.hidden) {
                    // 页面变为可见时，刷新数据
                    this.loadProviderStatuses();
                    this.refreshProxyKeysOnly();
                }
            });
        },

        // 检查用户身份验证状态
        async checkAuthentication() {
            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    return false;
                }

                const response = await fetch('/auth/verify', {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                });

                if (response.ok) {
                    const data = await response.json();
                    if (data.valid) {
                        // 更新用户信息
                        if (data.user) {
                            this.user = {
                                username: data.user.username || 'admin',
                                role: data.user.role || 'Admin'
                            };
                        }
                        return true;
                    } else {
                        localStorage.removeItem('authToken');
                        return false;
                    }
                } else {
                    localStorage.removeItem('authToken');
                    return false;
                }
            } catch (error) {
                console.error('Authentication check error:', error);
                localStorage.removeItem('authToken');
                return false;
            }
        },

        // 版本检测相关函数
        async checkVersion() {
            if (this.checkingVersion) return;

            this.checkingVersion = true;
            try {
                const response = await fetch('/health/version');
                if (response.ok) {
                    this.versionInfo = await response.json();
                } else {
                    console.warn('版本检查失败:', response.status);
                }
            } catch (error) {
                console.error('版本检查错误:', error);
            } finally {
                this.checkingVersion = false;
            }
        },

        openReleaseUrl() {
            if (this.versionInfo && this.versionInfo.releaseUrl) {
                window.open(this.versionInfo.releaseUrl, '_blank');
            }
        },

        // 用户设置相关函数
        closeUserSettingsModal() {
            this.showUserSettingsModal = false;
            this.resetUserForm();
        },

        resetUserForm() {
            this.userForm = {
                currentPassword: '',
                newUsername: '',
                newPassword: '',
                confirmPassword: ''
            };
        },

        async updateUserInfo() {
            // 验证表单
            if (!this.userForm.currentPassword) {
                this.showMessage('请输入当前密码', 'error');
                return;
            }

            if (this.userForm.newPassword && this.userForm.newPassword !== this.userForm.confirmPassword) {
                this.showMessage('新密码与确认密码不匹配', 'error');
                return;
            }

            if (this.userForm.newPassword && this.userForm.newPassword.length < 6) {
                this.showMessage('新密码长度至少6位', 'error');
                return;
            }

            // 如果没有要修改的内容
            if (!this.userForm.newUsername && !this.userForm.newPassword) {
                this.showMessage('请至少修改用户名或密码中的一项', 'error');
                return;
            }

            this.updatingUserInfo = true;

            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                // 构建请求数据
                const requestData = {
                    currentPassword: this.userForm.currentPassword
                };

                if (this.userForm.newUsername) {
                    requestData.newUsername = this.userForm.newUsername;
                }

                if (this.userForm.newPassword) {
                    requestData.newPassword = this.userForm.newPassword;
                }

                const response = await fetch('/auth/update-user', {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(requestData)
                });

                const data = await response.json();

                if (response.ok) {
                    // 更新本地用户信息
                    if (this.userForm.newUsername) {
                        this.user.username = this.userForm.newUsername;
                    }

                    // 如果修改了密码，立即退出到登录页
                    if (this.userForm.newPassword) {
                        this.showMessage('密码已修改，即将跳转到登录页...', 'success');
                        this.closeUserSettingsModal();

                        // 1.5秒后自动跳转到登录页
                        setTimeout(() => {
                            localStorage.removeItem('authToken');
                            window.location.href = '/login';
                        }, 1500);
                    } else {
                        // 只修改用户名，不需要重新登录
                        this.showMessage('用户信息更新成功', 'success');
                        this.closeUserSettingsModal();
                    }
                } else {
                    throw new Error(data.error || '更新用户信息失败');
                }
            } catch (error) {
                console.error('更新用户信息失败:', error);
                this.showMessage('更新失败: ' + error.message, 'error');
            } finally {
                this.updatingUserInfo = false;
            }
        },

        async loadSystemHealth() {
            this.loadingSystemHealth = true;
            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch("/admin/health/system", {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                });

                if (response.ok) {
                    this.systemHealth = await response.json();
                    // this.showMessage('系统健康状态已更新', 'success');
                } else {
                    throw new Error("获取系统健康状态失败");
                }
            } catch (error) {
                console.error(
                    "Failed to load system health:",
                    error,
                );
                this.showMessage(
                    "加载系统健康状态失败: " + error.message,
                    "error",
                );
            } finally {
                this.loadingSystemHealth = false;
            }
        },

        async loadProviderStatuses() {
            this.loadingProviderStatuses = true;
            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch("/admin/groups/manage", {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                });

                if (response.ok) {
                    const data = await response.json();
                    this.providerStatuses = data.groups || {};
                    this.filterProviders();
                    // this.showMessage('服务商分组状态已更新', 'success');
                } else {
                    throw new Error("获取服务商分组状态失败");
                }
            } catch (error) {
                console.error(
                    "Failed to load provider statuses:",
                    error,
                );
                this.showMessage(
                    "加载服务商分组状态失败: " + error.message,
                    "error",
                );
            } finally {
                this.loadingProviderStatuses = false;
            }
        },

        async refreshData() {
            // 手动刷新时刷新所有数据
            await this.refreshSystemData();
        },

        async refreshProviderHealth() {
            await this.loadProviderStatuses();
            this.lastUpdate = new Date();
        },

        // 加载所有配置的模型别名
        async loadAllConfiguredAliases() {
            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch('/admin/models/all-aliases', {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (response.ok) {
                    const data = await response.json();
                    this.allConfiguredAliases = data.aliases || [];
                } else {
                    console.error('加载所有配置别名失败:', response.status);
                }
            } catch (error) {
                console.error('加载所有配置别名异常:', error);
            }
        },

        // 清除无效密钥
        async clearInvalidKeys() {
            try {
                // 确认对话框
                const confirmed = await showConfirm('此操作将删除所有状态码为401的无效密钥，包括相关的验证记录和使用统计。\n\n确定要继续吗？', '确认清除无效密钥');
                if (!confirmed) {
                    return;
                }

                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                // 设置加载状态
                this.clearingInvalidKeys = true;

                const response = await fetch('/admin/keys/clear-invalid', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`
                    }
                });

                const result = await response.json();

                if (response.ok && result.success) {
                    // 显示操作结果
                    let message = `清除操作完成！\n`;
                    message += `• 清除密钥数量: ${result.cleared_keys_count}\n`;
                    if (result.affected_groups && result.affected_groups.length > 0) {
                        message += `• 受影响分组: ${result.affected_groups.join(', ')}\n`;
                    }
                    if (result.errors && result.errors.length > 0) {
                        message += `• 错误信息: ${result.errors.join('; ')}\n`;
                    }
                    message += `• 操作耗时: ${Math.round(result.duration_ms)}ms`;

                    await showAlert(message, 'success', '清除无效密钥完成');

                    // 刷新分组状态
                    await this.loadProviderStatuses();
                    this.lastUpdate = new Date();
                } else {
                    throw new Error(result.message || '清除无效密钥失败');
                }
            } catch (error) {
                console.error('清除无效密钥失败:', error);
                await showAlert(`清除无效密钥失败: ${error.message}`, 'error', '操作失败');
            } finally {
                // 恢复按钮状态
                this.clearingInvalidKeys = false;
            }
        },

        // 清除空白密钥的服务商分组
        async clearEmptyGroups() {
            try {
                // 确认对话框
                const confirmed = await showConfirm('此操作将标记删除所有没有密钥的服务商分组。\n\n确定要继续吗？', '确认清除空白分组');
                if (!confirmed) {
                    return;
                }

                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                // 设置加载状态
                this.clearingEmptyGroups = true;

                const response = await fetch('/admin/groups/clear-empty', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`
                    }
                });

                const result = await response.json();

                if (response.ok && result.success) {
                    // 显示操作结果
                    let message = `清除操作完成！\n`;
                    message += `• 清除分组数量: ${result.cleared_groups_count}\n`;
                    message += `• 检查分组总数: ${result.total_groups_checked}\n`;
                    if (result.cleared_groups && result.cleared_groups.length > 0) {
                        message += `• 清除的分组: ${result.cleared_groups.map(g => g.name).join(', ')}\n`;
                    }
                    if (result.errors && result.errors.length > 0) {
                        message += `• 错误信息: ${result.errors.join('; ')}\n`;
                    }
                    message += `• 操作耗时: ${Math.round(result.duration_ms)}ms`;

                    await showAlert(message, 'success', '清除空白分组完成');

                    // 刷新分组状态
                    await this.loadProviderStatuses();
                    this.lastUpdate = new Date();
                } else {
                    // 处理错误
                    const errorMessage = result.message || "清除空白分组失败";
                    console.error("Clear empty groups error:", result);
                    await showAlert(errorMessage, 'error', '清除空白分组失败');
                }
            } catch (error) {
                console.error("Clear empty groups failed:", error);
                await showAlert("清除空白分组失败: " + error.message, 'error', '操作失败');
            } finally {
                // 恢复按钮状态
                this.clearingEmptyGroups = false;
            }
        },

        // 导出分组配置
        async exportGroups() {
            try {
                let groupsToExport = [];

                if (this.selectedGroups.length === 0) {
                    // 如果没有选中任何分组，导出所有分组
                    const confirmed = await showConfirm('没有选中任何分组，是否导出所有分组？', '确认导出');
                    if (confirmed) {
                        groupsToExport = Object.keys(this.providerStatuses);
                    } else {
                        return;
                    }
                } else {
                    groupsToExport = this.selectedGroups;
                }

                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch('/admin/groups/export', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`
                    },
                    body: JSON.stringify({
                        group_ids: groupsToExport
                    })
                });

                if (response.ok) {
                    const blob = await response.blob();
                    const url = window.URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.style.display = 'none';
                    a.href = url;
                    a.download = `groups_config_${new Date().toISOString().split('T')[0]}.json`;
                    document.body.appendChild(a);
                    a.click();
                    window.URL.revokeObjectURL(url);
                    document.body.removeChild(a);

                    await showAlert(`成功导出 ${groupsToExport.length} 个分组的配置`, 'success', '导出成功');
                } else {
                    const errorData = await response.json();
                    await showAlert('导出失败: ' + (errorData.error || '未知错误'), 'error', '导出失败');
                }
            } catch (error) {
                console.error('Export error:', error);
                await showAlert('导出失败: ' + error.message, 'error', '导出失败');
            }
        },

        // 处理文件选择
        handleFileSelect(event) {
            this.selectedFile = event.target.files[0];
        },

        // 导入分组配置
        async importGroups() {
            if (!this.selectedFile) {
                await showAlert('请选择配置文件', 'warning', '提示');
                return;
            }

            this.importing = true;

            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const formData = new FormData();
                formData.append('config_file', this.selectedFile);

                const response = await fetch('/admin/groups/import', {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${token}`
                    },
                    body: formData
                });

                const data = await response.json();

                if (data.success) {
                    let message = `成功导入 ${data.imported_count}/${data.total_groups} 个分组`;

                    if (data.errors && data.errors.length > 0) {
                        message += '\n\n错误信息:\n' + data.errors.join('\n');
                    }

                    await showAlert(message, data.errors && data.errors.length > 0 ? 'warning' : 'success', '导入结果');

                    // 重新加载服务商状态
                    await this.loadProviderStatuses();

                    // 关闭模态框并重置状态
                    this.showImportModal = false;
                    this.selectedFile = null;

                    // 重置文件输入
                    const fileInput = document.querySelector('input[type="file"]');
                    if (fileInput) {
                        fileInput.value = '';
                    }
                } else {
                    await showAlert('导入失败: ' + (data.error || '未知错误'), 'error', '导入失败');
                }
            } catch (error) {
                console.error('Import error:', error);
                await showAlert('导入失败: ' + error.message, 'error', '导入失败');
            } finally {
                this.importing = false;
            }
        },

        async checkProviderHealth(groupId) {
            try {
                const response = await fetch(
                    `/admin/health/providers/${groupId}`,
                );
                if (response.ok) {
                    const data = await response.json();
                    this.providerStatuses[groupId] = data;
                }
            } catch (error) {
                console.error(
                    "Failed to check provider health:",
                    error,
                );
            }
        },

        async loadProviderModels() {
            this.loadingModels = true;
            try {
                const url = this.selectedProvider
                    ? `/admin/models/${this.selectedProvider}`
                    : "/admin/models";

                const response = await fetch(url);
                if (response.ok) {
                    const data = await response.json();
                    this.providerModels = data.data || {};
                } else {
                    // 如果API调用失败，清空模型列表并显示错误信息
                    this.providerModels = {};
                    const errorData = await response.json().catch(() => ({}));
                    this.showMessage(
                        "获取模型列表失败: " + (errorData.error || errorData.message || "服务器错误"),
                        "error"
                    );
                }
            } catch (error) {
                console.error("Failed to load models:", error);
                // 网络错误时也清空模型列表并显示错误信息
                this.providerModels = {};
                this.showMessage(
                    "获取模型列表失败: " + error.message,
                    "error"
                );
            } finally {
                this.loadingModels = false;
            }
        },

        // 移除自动刷新功能
        // toggleAutoRefresh() {
        //     this.autoRefresh = !this.autoRefresh;
        //     if (this.autoRefresh) {
        //         this.startAutoRefresh();
        //     } else {
        //         this.stopAutoRefresh();
        //     }
        // },

        // 密钥状态相关方法
        async loadKeyStatus() {
            this.loadingKeyStatus = true;
            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch("/admin/keys/status", {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                });
                if (response.ok) {
                    const data = await response.json();
                    if (data.success) {
                        // 计算总计数据
                        let totalKeys = 0;
                        let totalValid = 0;
                        let totalInvalid = 0;

                        for (const groupId in data.data) {
                            const groupData = data.data[groupId];
                            totalKeys += groupData.total_keys;
                            totalValid += groupData.valid_keys;
                            totalInvalid += groupData.invalid_keys;
                        }

                        this.keyStatus = {
                            total_keys: totalKeys,
                            total_valid: totalValid,
                            total_invalid: totalInvalid,
                            last_updated: new Date().toLocaleString(
                                "zh-CN",
                            ),
                            groups: data.data,
                        };
                    }
                }
            } catch (error) {
                console.error("Failed to load key status:", error);
            } finally {
                this.loadingKeyStatus = false;
            }
        },

        async refreshKeyStatus() {
            await this.loadKeyStatus();
            // 移除自动加载验证状态，改为手动触发
            // await this.loadPersistedValidationStatus();
        },

        // 加载所有分组的持久化验证状态
        async loadPersistedValidationStatus(showMessage = true) {
            this.loadingValidation = true;
            try {
                // 获取所有分组ID
                const groupIds = Object.keys(
                    this.providerStatuses || {},
                );
                let processedGroups = 0;

                // 为每个分组加载验证状态
                for (const groupId of groupIds) {
                    try {
                        const response = await fetch(
                            `/admin/keys/validation/${groupId}`,
                        );
                        if (response.ok) {
                            const data = await response.json();
                            if (data.success) {
                                // 检查是否有实际的验证记录
                                const hasValidationRecords = data.validation_status && 
                                    Object.keys(data.validation_status).length > 0;
                                
                                if (hasValidationRecords) {
                                    // 只有当存在验证记录时，才使用后端计算的密钥统计信息
                                    const validCount = data.valid_keys || 0;
                                    const invalidCount = data.invalid_keys || 0;
                                    const totalCount = data.total_keys || 0;
                                    const unknownCount = data.unknown_keys || 0;

                                    // 更新密钥状态数据
                                    if (!this.keyStatus.groups) {
                                        this.keyStatus.groups = {};
                                    }
                                    this.keyStatus.groups[groupId] = {
                                        valid_keys: validCount,
                                        invalid_keys: invalidCount,
                                        unknown_keys: unknownCount, // 新增：未检测密钥数量
                                        total_keys: totalCount,
                                        last_validated: data.last_validated || new Date().toISOString(),
                                    };
                                }
                                // 如果没有验证记录，保持原有的provider数据，不做覆盖
                            }
                        }
                        processedGroups++;
                    } catch (error) {
                        console.error(
                            `Failed to load validation status for group ${groupId}:`,
                            error,
                        );
                        processedGroups++;
                    }
                }

                // 重新计算总计数据
                if (this.keyStatus.groups) {
                    let totalKeys = 0;
                    let totalValid = 0;
                    let totalInvalid = 0;

                    for (const groupData of Object.values(
                        this.keyStatus.groups,
                    )) {
                        totalKeys += groupData.total_keys || 0;
                        totalValid += groupData.valid_keys || 0;
                        totalInvalid += groupData.invalid_keys || 0;
                    }

                    this.keyStatus.total_keys = totalKeys;
                    this.keyStatus.total_valid = totalValid;
                    this.keyStatus.total_invalid = totalInvalid;
                    this.keyStatus.last_updated =
                        new Date().toLocaleString();
                }

                // 显示完成消息（仅在手动调用时显示）
                if (showMessage) {
                    this.showMessage(
                        `已检查 ${processedGroups} 个分组的密钥验证状态`,
                        "success",
                    );
                }
            } catch (error) {
                console.error(
                    "Failed to load persisted validation status:",
                    error,
                );
                this.showMessage(
                    "加载密钥验证状态失败: " + error.message,
                    "error",
                );
            } finally {
                this.loadingValidation = false;
            }
        },

        // 服务商筛选和管理方法
        filterProviders(resetPage = true) {
            let providers = Object.entries(this.providerStatuses);

            // 按创建时间倒序排序（后端已经排序，但为了确保一致性）
            providers.sort(([, a], [, b]) => {
                const aTime = a.created_at
                    ? new Date(a.created_at)
                    : new Date(0);
                const bTime = b.created_at
                    ? new Date(b.created_at)
                    : new Date(0);
                return bTime - aTime; // 倒序
            });

            // 搜索筛选
            if (this.providerSearchQuery.trim()) {
                const query =
                    this.providerSearchQuery.toLowerCase();
                providers = providers.filter(
                    ([groupId, provider]) =>
                        provider.group_name
                            .toLowerCase()
                            .includes(query) ||
                        provider.provider_type
                            .toLowerCase()
                            .includes(query) ||
                        groupId.toLowerCase().includes(query) ||
                        (provider.base_url && provider.base_url
                            .toLowerCase()
                            .includes(query)),
                );
            }

            // 状态筛选
            if (this.providerStatusFilter) {
                providers = providers.filter(
                    ([groupId, provider]) => {
                        if (
                            this.providerStatusFilter === "healthy"
                        ) {
                            return provider.healthy;
                        } else if (
                            this.providerStatusFilter ===
                            "unhealthy"
                        ) {
                            return !provider.healthy;
                        }
                        return true;
                    },
                );
            }

            // 类型筛选
            if (this.providerTypeFilter) {
                providers = providers.filter(
                    ([groupId, provider]) =>
                        provider.provider_type ===
                        this.providerTypeFilter,
                );
            }

            // 启用状态筛选
            if (this.providerEnabledFilter) {
                providers = providers.filter(
                    ([groupId, provider]) => {
                        if (this.providerEnabledFilter === "enabled") {
                            return provider.enabled !== false;
                        } else if (this.providerEnabledFilter === "disabled") {
                            return provider.enabled === false;
                        }
                        return true;
                    },
                );
            }

            // 转换为对象格式，保持排序顺序
            this.filteredProviders = {};
            providers.forEach(([groupId, provider]) => {
                this.filteredProviders[groupId] = provider;
            });

            // 重置页码（可选）
            if (resetPage) {
                this.providerPage = 1;
            }
        },

        // 获取服务商卡片的CSS类
        getProviderCardClass(provider) {
            // 首先判断是否禁用
            if (provider.enabled === false) {
                return 'border-gray-300 bg-gray-100';
            }

            // 如果启用，再判断健康状态
            if (provider.healthy) {
                return 'border-green-200 bg-green-50';
            } else {
                return 'border-red-200 bg-red-50';
            }
        },

        async validateAllKeys() {
            this.validatingAllKeys = true;
            try {
                await this.loadKeyStatus();
                // 检查是否有失效密钥
                const hasInvalidKeys =
                    this.keyStatus.total_invalid > 0;
                if (hasInvalidKeys) {
                    // 如果有失效密钥，可以选择打开分组管理页面
                    if (
                        confirm(
                            `发现 ${this.keyStatus.total_invalid} 个失效密钥，是否打开分组管理页面进行处理？`,
                        )
                    ) {
                        window.open("/groups", "_blank");
                    }
                } else {
                    alert("所有密钥都是有效的！");
                }
            } catch (error) {
                console.error("Failed to validate keys:", error);
                alert("验证密钥时出错，请稍后重试");
            } finally {
                this.validatingAllKeys = false;
            }
        },

        // 验证单个分组的密钥
        async validateGroupKeys(groupId, provider) {
            // 确保 validatingGroups 对象存在并设置验证状态
            if (!this.validatingGroups) {
                this.validatingGroups = {};
            }
            this.validatingGroups[groupId] = true;

            // 强制更新UI
            this.$nextTick(() => {
                this.validatingGroups = {
                    ...this.validatingGroups,
                };
            });

            try {
                // 获取分组的完整数据
                const response = await fetch(
                    "/admin/groups/manage",
                );
                if (!response.ok) {
                    throw new Error("Failed to fetch group data");
                }

                const data = await response.json();
                const groupData = data.groups[groupId];

                if (
                    !groupData ||
                    !groupData.api_keys ||
                    groupData.api_keys.length === 0
                ) {
                    alert("该分组没有配置API密钥");
                    return;
                }

                // 过滤掉空密钥
                const validKeys = groupData.api_keys.filter(
                    (key) => key && key.trim().length > 0,
                );

                if (validKeys.length === 0) {
                    alert("该分组没有有效的API密钥");
                    return;
                }

                // 发送验证请求
                const validateResponse = await fetch(
                    `/admin/keys/validate/${groupId}`,
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify({
                            api_keys: validKeys,
                        }),
                    },
                );

                if (!validateResponse.ok) {
                    throw new Error("验证请求失败");
                }

                const result = await validateResponse.json();

                if (result.success) {
                    const validCount = result.valid_keys || 0;
                    const invalidCount = result.invalid_keys || 0;
                    const totalCount = result.total_keys || 0;

                    // 更新本地密钥状态数据
                    if (!this.keyStatus.groups) {
                        this.keyStatus.groups = {};
                    }
                    this.keyStatus.groups[groupId] = {
                        valid_keys: validCount,
                        invalid_keys: invalidCount,
                        unknown_keys: 0, // 验证完成后，所有密钥都已检测，没有未知状态
                        total_keys: totalCount,
                        last_validated: new Date().toISOString(),
                    };

                    // 显示结果
                    const message =
                        `分组 "${provider.group_name}" 密钥验证完成：\n\n` +
                        `✅ 有效密钥：${validCount}\n` +
                        `❌ 无效密钥：${invalidCount}\n` +
                        `📊 总计：${totalCount}\n\n` +
                        `验证结果已保存到密钥管理列表中`;

                    alert(message);

                    // 刷新服务商状态和密钥状态
                    await this.loadProviderStatuses();
                    await this.refreshKeyStatus();
                } else {
                    throw new Error(result.message || "验证失败");
                }
            } catch (error) {
                console.error("验证分组密钥失败:", error);
                alert(`验证分组密钥失败：${error.message}`);
            } finally {
                // 清除验证状态
                if (this.validatingGroups) {
                    this.validatingGroups[groupId] = false;
                    // 强制更新UI
                    this.$nextTick(() => {
                        this.validatingGroups = {
                            ...this.validatingGroups,
                        };
                    });
                }
            }
        },

        showProviderDetails(groupId, provider) {
            const keyStatusData = this.keyStatus.groups[groupId];
            let detailsHtml = `
                        <div class="space-y-4">
                            <div>
                                <h3 class="text-lg font-semibold">${provider.group_name} (${groupId})</h3>
                                <p class="text-sm text-gray-600">${provider.provider_type.toUpperCase()}</p>
                            </div>

                            <div class="grid grid-cols-2 gap-4 text-sm">
                                <div>
                                    <span class="font-medium">状态:</span>
                                    <span class="${provider.healthy ? "text-green-600" : "text-red-600"}">${provider.healthy ? "健康" : "异常"}</span>
                                </div>
                                <div>
                                    <span class="font-medium">响应时间:</span>
                                    <span>${this.formatResponseTime(provider.response_time)}</span>
                                </div>
                                <div>
                                    <span class="font-medium">总密钥数:</span>
                                    <span>${provider.total_keys}</span>
                                </div>
                                <div>
                                    <span class="font-medium">活跃密钥:</span>
                                    <span>${provider.active_keys}</span>
                                </div>
                            </div>
                    `;

            if (keyStatusData) {
                detailsHtml += `
                            <div class="border-t pt-4">
                                <h4 class="font-medium mb-2">密钥验证状态:</h4>
                                <div class="grid grid-cols-3 gap-4 text-sm">
                                    <div class="text-center">
                                        <div class="text-lg font-semibold text-green-600">${keyStatusData.valid_keys}</div>
                                        <div class="text-gray-600">有效</div>
                                    </div>
                                    <div class="text-center">
                                        <div class="text-lg font-semibold text-red-600">${keyStatusData.invalid_keys}</div>
                                        <div class="text-gray-600">无效</div>
                                    </div>
                                    <div class="text-center">
                                        <div class="text-lg font-semibold text-gray-600">${keyStatusData.total_keys}</div>
                                        <div class="text-gray-600">总计</div>
                                    </div>
                                </div>
                                <p class="text-xs text-gray-500 mt-2">测试模型: ${keyStatusData.test_model}</p>
                            </div>
                        `;
            }

            if (provider.last_error) {
                detailsHtml += `
                            <div class="border-t pt-4">
                                <h4 class="font-medium mb-2 text-red-600">错误信息:</h4>
                                <p class="text-sm text-red-700 bg-red-50 p-2 rounded">${provider.last_error}</p>
                            </div>
                        `;
            }

            detailsHtml += "</div>";

            // 这里可以使用一个模态框来显示详情，暂时使用alert
            const tempDiv = document.createElement("div");
            tempDiv.innerHTML = detailsHtml;
            alert(
                `${provider.group_name} 详细信息\n\n状态: ${provider.healthy ? "健康" : "异常"}\n响应时间: ${this.formatResponseTime(provider.response_time)}\n密钥: ${provider.active_keys}/${provider.total_keys}`,
            );
        },

        // 分组管理方法
        openCreateGroupModal() {
            this.resetGroupForm();
            this.showCreateGroupModal = true;
        },

        // 强制刷新表单UI显示
        forceRefreshFormUI() {
            setTimeout(() => {
                // 获取所有需要更新的表单元素
                const formElements = [
                    {
                        selector:
                            'input[x-model="groupFormData.rpm_limit"]',
                        value: this.groupFormData.rpm_limit,
                        event: "input",
                    },
                    {
                        selector:
                            'input[x-model="groupFormData.use_native_response"]',
                        value: this.groupFormData
                            .use_native_response,
                        event: "change",
                        isCheckbox: true,
                    },
                    {
                        selector:
                            'input[x-model="groupFormData.timeout"]',
                        value: this.groupFormData.timeout,
                        event: "input",
                    },
                    {
                        selector:
                            'input[x-model="groupFormData.max_retries"]',
                        value: this.groupFormData.max_retries,
                        event: "input",
                    },
                ];

                formElements.forEach((element) => {
                    const el = document.querySelector(
                        element.selector,
                    );
                    if (el) {
                        if (element.isCheckbox) {
                            el.checked = Boolean(element.value);
                        } else {
                            el.value = element.value;
                        }
                        el.dispatchEvent(
                            new Event(element.event, {
                                bubbles: true,
                            }),
                        );
                    }
                });
            }, 100);
        },

        async editGroup(groupId, provider) {
            this.editingGroupId = groupId;

            try {
                // 从分组管理接口获取完整的分组数据
                const response = await fetch(
                    "/admin/groups/manage",
                );
                if (!response.ok) {
                    throw new Error("Failed to fetch group data");
                }

                const data = await response.json();
                const fullGroupData = data.groups[groupId];

                if (!fullGroupData) {
                    throw new Error("Group data not found");
                }

                // 确保API密钥数组至少有一个空元素
                let apiKeys = [""];
                if (
                    fullGroupData.api_keys &&
                    Array.isArray(fullGroupData.api_keys) &&
                    fullGroupData.api_keys.length > 0
                ) {
                    apiKeys = [...fullGroupData.api_keys];
                }

                this.groupFormData = {
                    group_id: groupId,
                    name: fullGroupData.group_name || "",
                    provider_type:
                        fullGroupData.provider_type || "",
                    base_url: fullGroupData.base_url || "",
                    enabled: fullGroupData.enabled !== false,
                    timeout: parseInt(fullGroupData.timeout) || 30,
                    max_retries:
                        parseInt(fullGroupData.max_retries) || 3,
                    rotation_strategy:
                        fullGroupData.rotation_strategy ||
                        "round_robin",
                    api_keys: apiKeys,
                    models: fullGroupData.models || [], // 使用从后端获取的模型数据
                    use_native_response: Boolean(
                        fullGroupData.use_native_response,
                    ),
                    rpm_limit:
                        parseInt(fullGroupData.rpm_limit) || 0,
                    test_model: fullGroupData.test_model || "",
                    proxy_enabled: Boolean(fullGroupData.proxy_enabled),
                    proxy_config: fullGroupData.proxy_config ? {
                        type: fullGroupData.proxy_config.type || "http",
                        host: fullGroupData.proxy_config.host || "",
                        port: parseInt(fullGroupData.proxy_config.port) || 8080,
                        username: fullGroupData.proxy_config.username || "",
                        password: fullGroupData.proxy_config.password || "",
                        bypass_local: fullGroupData.proxy_config.bypass_local !== undefined ? fullGroupData.proxy_config.bypass_local : true,
                        bypass_domains: fullGroupData.proxy_config.bypass_domains || []
                    } : {
                        type: "http",
                        host: "",
                        port: 8080,
                        username: "",
                        password: "",
                        bypass_local: true,
                        bypass_domains: []
                    },
                    proxy_bypass_domains_text: fullGroupData.proxy_config && fullGroupData.proxy_config.bypass_domains ?
                        fullGroupData.proxy_config.bypass_domains.join('\n') : "",
                };

                this.modelsText = (fullGroupData.models || []).join(
                    "\n",
                );

                // 确保modelsText和groupFormData.models同步
                if (this.modelsText.trim()) {
                    this.syncTextToModels();
                }

                // 加载JSON请求参数
                if (
                    fullGroupData.parameter_overrides &&
                    Object.keys(fullGroupData.parameter_overrides)
                        .length > 0
                ) {
                    this.requestParamsText = JSON.stringify(
                        fullGroupData.parameter_overrides,
                        null,
                        2,
                    );
                } else {
                    this.requestParamsText = "";
                }
                // 加载JSON请求头
                if (
                    fullGroupData.headers &&
                    Object.keys(fullGroupData.headers)
                        .length > 0
                ) {
                    this.headersText = JSON.stringify(
                        fullGroupData.headers,
                        null,
                        2,
                    );
                } else {
                    this.headersText = "";
                }

                // 加载模型映射
                this.modelMappings = [];
                if (
                    fullGroupData.model_aliases &&
                    Object.keys(fullGroupData.model_aliases)
                        .length > 0
                ) {
                    for (const [alias, original] of Object.entries(
                        fullGroupData.model_aliases,
                    )) {
                        this.modelMappings.push({
                            alias: alias,
                            original: original,
                        });
                    }
                }

                // 使用setTimeout确保所有数据都已经设置完成
                setTimeout(() => {
                    // 强制触发响应式更新
                    this.modelMappings = [...this.modelMappings];
                }, 100);

                this.selectedKeys = [];
                this.keyPage = 1;
                this.availableModels = [];
                this.filteredModels = [];
                this.modelSearchQuery = "";
                this.keyValidationStatus = {};
                this.invalidKeyIndexes = [];
                this.requestParamsValidationMessage = "";
                this.requestParamsValidationError = false;

                this.showEditGroupModal = true;

                // 延迟刷新表单UI，确保模态框完全显示后再刷新
                setTimeout(() => {
                    this.forceRefreshFormUI();
                }, 200);

                // 自动加载验证状态以确保一键删除功能正常工作
                await this.loadKeyValidationStatus(groupId);
            } catch (error) {
                console.error("获取分组数据失败:", error);
                alert("获取分组数据失败，请稍后重试");
            }
        },

        async toggleGroup(groupId, provider) {
            try {
                const response = await fetch(
                    `/admin/groups/${groupId}/toggle`,
                    {
                        method: "POST",
                    },
                );

                const data = await response.json();

                if (response.ok) {
                    this.showMessage(data.message, "success");
                    // 重新加载服务商状态以反映分组状态变化
                    await this.loadProviderStatuses();
                } else {
                    this.showMessage(
                        data.message || "操作失败",
                        "error",
                    );
                }
            } catch (error) {
                this.showMessage(
                    "网络错误: " + error.message,
                    "error",
                );
            }
        },

        async deleteGroup(groupId, provider) {
            if (
                !confirm(
                    `确定要删除分组 "${provider.group_name}" 吗？此操作不可恢复。`,
                )
            ) {
                return;
            }

            try {
                const response = await fetch(
                    `/admin/groups/${groupId}`,
                    {
                        method: "DELETE",
                    },
                );

                if (response.ok) {
                    this.showMessage("分组删除成功", "success");
                    // 重新加载服务商状态以移除已删除的分组
                    await this.loadProviderStatuses();
                } else {
                    // 只有非204响应才尝试解析JSON
                    let errorMessage = "删除失败";
                    if (response.status !== 204) {
                        try {
                            const data = await response.json();
                            errorMessage = data.message || errorMessage;
                        } catch (e) {
                            // 如果解析JSON失败，使用默认错误消息
                        }
                    }
                    this.showMessage(errorMessage, "error");
                }
            } catch (error) {
                this.showMessage(
                    "网络错误: " + error.message,
                    "error",
                );
            }
        },

        async submitGroupForm() {
            this.submittingGroup = true;
            try {
                // 保存当前的页面状态（用于编辑模式保持位置）
                const currentPage = this.providerPage;
                const currentSearchQuery = this.providerSearchQuery;
                const currentStatusFilter =
                    this.providerStatusFilter;
                const currentTypeFilter = this.providerTypeFilter;
                const currentEnabledFilter = this.providerEnabledFilter;

                // 处理模型列表 - 只使用文本框输入的模型（统一管理）
                const manualModels = this.modelsText
                    .split("\n")
                    .map((line) => line.trim())
                    .filter((line) => line.length > 0);

                // 去重处理模型列表
                const allModels = [...new Set(manualModels)];
                this.groupFormData.models = allModels;

                // 过滤空的API密钥并去重
                const validKeys = this.groupFormData.api_keys
                    .map(key => key.trim())
                    .filter(key => key.length > 0);

                // 去重处理
                const uniqueKeys = [...new Set(validKeys)];
                this.groupFormData.api_keys = uniqueKeys;

                // 如果发现重复密钥，显示提示
                if (validKeys.length !== uniqueKeys.length) {
                    const duplicateCount = validKeys.length - uniqueKeys.length;
                    this.showMessage(
                        `检测到 ${duplicateCount} 个重复密钥已自动去除`,
                        "success"
                    );
                }

                // 处理JSON请求参数
                if (this.requestParamsText.trim()) {
                    try {
                        this.groupFormData.request_params =
                            JSON.parse(
                                this.requestParamsText.trim(),
                            );
                        this.requestParamsValidationMessage = "";
                        this.requestParamsValidationError = false;
                    } catch (e) {
                        this.requestParamsValidationMessage =
                            "JSON格式错误: " + e.message;
                        this.requestParamsValidationError = true;
                        this.submittingGroup = false;
                        return;
                    }
                } else {
                    this.groupFormData.request_params = {};
                }

                // 处理JSON请求头
                if (this.headersText.trim()) {
                    try {
                        this.groupFormData.headers =
                            JSON.parse(
                                this.headersText.trim(),
                            );
                        this.headersValidationMessage = "";
                        this.headersValidationError = false;
                    } catch (e) {
                        this.headersValidationMessage =
                            "JSON格式错误: " + e.message;
                        this.headersValidationError = true;
                        this.submittingGroup = false;
                        return;
                    }
                } else {
                    this.groupFormData.headers = {};
                }

                // 处理模型映射
                const modelMappings = {};
                for (const mapping of this.modelMappings) {
                    if (
                        mapping.alias &&
                        mapping.alias.trim() &&
                        mapping.original &&
                        mapping.original.trim()
                    ) {
                        const alias = mapping.alias.trim();
                        const original = mapping.original.trim();

                        // 检查是否已存在相同的别名
                        if (modelMappings[alias] && modelMappings[alias] !== original) {
                            // 如果别名已存在且映射到不同的原始模型，给出警告
                            const message = `警告：别名 "${alias}" 已映射到模型 "${modelMappings[alias]}"，现在将被覆盖为 "${original}"。\n\n如果您想要多个模型都使用相同的别名，建议：\n1. 在不同的分组中分别设置映射\n2. 或者使用不同的别名（如 gpt-4-v1, gpt-4-v2）`;

                            if (!confirm(message + '\n\n是否继续保存？')) {
                                return; // 用户取消保存
                            }
                        }

                        modelMappings[alias] = original;
                    }
                }
                this.groupFormData.model_mappings = modelMappings;

                // 确保数字字段是正确的类型
                this.groupFormData.timeout =
                    parseInt(this.groupFormData.timeout) || 30;
                this.groupFormData.max_retries =
                    parseInt(this.groupFormData.max_retries) || 3;
                this.groupFormData.rpm_limit =
                    parseInt(this.groupFormData.rpm_limit) || 0;

                const url = this.showCreateGroupModal
                    ? "/admin/groups"
                    : `/admin/groups/${this.editingGroupId}`;
                const method = this.showCreateGroupModal
                    ? "POST"
                    : "PUT";

                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                // 将前端字段名映射到后端期望的字段名
                const requestData = {
                    id: this.groupFormData.group_id || "",
                    group_name: this.groupFormData.name,
                    provider_type: this.groupFormData.provider_type,
                    base_url: this.groupFormData.base_url || "",
                    api_keys: this.groupFormData.api_keys || [],
                    models: this.groupFormData.models || [],
                    model_aliases: this.groupFormData.model_mappings || {},
                    parameter_overrides: this.groupFormData.request_params || {},
                    headers: this.groupFormData.headers || {},
                    balance_policy: this.groupFormData.rotation_strategy || "round_robin",
                    retry_count: this.groupFormData.max_retries || 3,
                    timeout: this.groupFormData.timeout || 60,
                    rpm_limit: this.groupFormData.rpm_limit || 0,
                    test_model: this.groupFormData.test_model || null,
                    priority: this.groupFormData.priority || 0,
                    enabled: this.groupFormData.enabled !== undefined ? this.groupFormData.enabled : true,
                    fake_streaming: this.groupFormData.fake_streaming || false, // 添加假流配置
                    proxy_enabled: this.groupFormData.proxy_enabled || false,
                    proxy_config: this.groupFormData.proxy_enabled ? {
                        type: this.groupFormData.proxy_config.type || "http",
                        host: this.groupFormData.proxy_config.host || "",
                        port: parseInt(this.groupFormData.proxy_config.port) || 8080,
                        username: this.groupFormData.proxy_config.username || "",
                        password: this.groupFormData.proxy_config.password || "",
                        bypass_local: this.groupFormData.proxy_config.bypass_local !== undefined ? this.groupFormData.proxy_config.bypass_local : true,
                        bypass_domains: this.groupFormData.proxy_bypass_domains_text ?
                            this.groupFormData.proxy_bypass_domains_text.split('\n').map(d => d.trim()).filter(d => d) : []
                    } : null
                };

                // 验证必需字段
                if (this.showCreateGroupModal && (!requestData.id || !requestData.id.trim())) {
                    throw new Error("分组ID不能为空");
                }
                if (!requestData.group_name || !requestData.group_name.trim()) {
                    throw new Error("分组名称不能为空");
                }
                if (!requestData.provider_type || !requestData.provider_type.trim()) {
                    throw new Error("服务商类型不能为空");
                }
                if (!requestData.api_keys || requestData.api_keys.length === 0 ||
                    requestData.api_keys.every(key => !key || !key.trim())) {
                    throw new Error("至少需要提供一个API密钥");
                }

                const response = await fetch(url, {
                    method: method,
                    headers: {
                        "Content-Type": "application/json",
                        'Authorization': `Bearer ${token}`
                    },
                    body: JSON.stringify(requestData),
                });

                const data = await response.json();

                if (response.ok) {
                    this.showMessage(data.message, "success");

                    // 无论是创建还是编辑模式，都重新加载服务商状态以确保数据一致性
                    // 特别是在编辑时修改了API密钥列表的情况下，需要更新密钥数量显示
                    this.closeGroupModal();
                    await this.loadProviderStatuses();
                    
                    // 重新加载密钥状态以确保密钥数量正确显示
                    await this.loadKeyStatus();

                    // 如果是编辑模式，恢复页面状态以保持用户体验
                    if (!this.showCreateGroupModal && this.editingGroupId) {
                        this.providerPage = currentPage;
                        this.providerSearchQuery = currentSearchQuery;
                        this.providerStatusFilter = currentStatusFilter;
                        this.providerTypeFilter = currentTypeFilter;
                        this.providerEnabledFilter = currentEnabledFilter;
                        
                        // 重新过滤服务商以反映更新，但不重置页面位置
                        this.filterProviders(false);
                    }

                    // 额外的数据验证，确保保存的数据正确
                    setTimeout(async () => {
                        if (
                            this.editingGroupId &&
                            this.providerStatuses[
                            this.editingGroupId
                            ]
                        ) {
                            const savedData =
                                this.providerStatuses[
                                this.editingGroupId
                                ];
                        }
                    }, 500);
                } else {
                    this.showMessage(
                        data.message || "操作失败",
                        "error",
                    );
                }
            } catch (error) {
                this.showMessage(
                    "网络错误: " + error.message,
                    "error",
                );
            } finally {
                this.submittingGroup = false;
            }
        },

        closeGroupModal() {
            this.showCreateGroupModal = false;
            this.showEditGroupModal = false;
            this.showBatchAddModal = false;
            this.editingGroupId = "";
            this.resetGroupForm();
        },

        resetGroupForm() {
            this.groupFormData = {
                group_id: "",
                name: "",
                provider_type: "",
                base_url: "",
                enabled: true,
                timeout: 30,
                max_retries: 3,
                rotation_strategy: "round_robin",
                api_keys: [""],
                models: [],
                use_native_response: false,
                rpm_limit: 0,
                request_params: {},
                model_mappings: {},
                headers: {},
                priority: 0,
                fake_streaming: false, // 假流模式配置
                proxy_enabled: false,
                proxy_config: {
                    type: "http",
                    host: "",
                    port: 8080,
                    username: "",
                    password: "",
                    bypass_local: true,
                    bypass_domains: []
                },
                proxy_bypass_domains_text: "",
            };
            this.modelsText = "";
            this.selectedKeys = [];
            this.keyPage = 1;
            this.availableModels = [];
            this.filteredModels = [];
            this.modelSearchQuery = "";
            this.batchKeysText = "";
            this.keyValidationStatus = {};
            this.invalidKeyIndexes = [];
            this.forcingKeyStatus = {};
            this.bulkDeletingInvalidKeys = false;

            // 重置JSON参数相关字段
            this.requestParamsText = "";
            this.showRequestParamsHelp = false;
            this.requestParamsValidationMessage = "";
            this.requestParamsValidationError = false;
            // 重置JSON请求头相关字段
            this.headersText = "";
            this.showHeadersHelp = false;
            this.headersValidationMessage = "";
            this.headersValidationError = false;

            // 重置模型映射相关字段
            this.modelMappings = [];
            this.showModelMappingHelp = false;
        },

        // 服务商类型变化时自动填充Base URL
        onProviderTypeChange() {
            const defaultBaseUrls = {
                openai: "https://api.openai.com/v1",
                openai_responses: "https://api.openai.com/v1",
                anthropic: "https://api.anthropic.com",
                gemini: "https://generativelanguage.googleapis.com",
                azure_openai:
                    "https://your-resource-name.openai.azure.com",
                openrouter: "https://openrouter.ai/api/v1",
            };

            const providerType = this.groupFormData.provider_type;
            if (providerType && defaultBaseUrls[providerType]) {
                // 只有当Base URL为空或者是默认值时才自动填充
                const currentBaseUrl = this.groupFormData.base_url;
                const isDefaultUrl =
                    Object.values(defaultBaseUrls).includes(
                        currentBaseUrl,
                    );

                if (!currentBaseUrl || isDefaultUrl) {
                    this.groupFormData.base_url =
                        defaultBaseUrls[providerType];
                }
            }
        },

        addGroupApiKey() {
            this.groupFormData.api_keys.push("");
        },

        // 检查密钥是否重复的辅助函数
        checkDuplicateKeys(keys) {
            const keyMap = new Map();
            const duplicates = [];
            const unique = [];

            keys.forEach((key, index) => {
                const trimmedKey = key.trim();
                if (trimmedKey.length === 0) return;

                if (keyMap.has(trimmedKey)) {
                    duplicates.push({
                        key: trimmedKey,
                        indexes: [keyMap.get(trimmedKey), index]
                    });
                } else {
                    keyMap.set(trimmedKey, index);
                    unique.push(trimmedKey);
                }
            });

            return {
                duplicates,
                unique,
                hasDuplicates: duplicates.length > 0
            };
        },

        // 去重密钥数组
        deduplicateKeys(keys) {
            const seen = new Set();
            return keys.filter(key => {
                const trimmedKey = key.trim();
                if (trimmedKey.length === 0) return false;
                if (seen.has(trimmedKey)) return false;
                seen.add(trimmedKey);
                return true;
            });
        },

        // 检查当前密钥列表中的重复项
        checkCurrentKeyDuplicates() {
            const duplicateIndexes = new Set();
            const seen = new Map();

            this.groupFormData.api_keys.forEach((key, index) => {
                const trimmedKey = key.trim();
                if (trimmedKey.length === 0) return;

                if (seen.has(trimmedKey)) {
                    // 标记当前索引和之前的索引为重复
                    duplicateIndexes.add(index);
                    duplicateIndexes.add(seen.get(trimmedKey));
                } else {
                    seen.set(trimmedKey, index);
                }
            });

            return duplicateIndexes;
        },

        // 移除所有重复的密钥，只保留第一个
        removeDuplicateKeys() {
            const originalLength = this.groupFormData.api_keys.length;
            const validKeys = this.groupFormData.api_keys.filter(key => key.trim().length > 0);
            const uniqueKeys = this.deduplicateKeys(validKeys);

            // 如果所有密钥都被去重了，至少保留一个空项
            if (uniqueKeys.length === 0) {
                this.groupFormData.api_keys = [""];
            } else {
                this.groupFormData.api_keys = uniqueKeys;
            }

            const removedCount = originalLength - this.groupFormData.api_keys.length;
            if (removedCount > 0) {
                this.showMessage(`已移除 ${removedCount} 个重复密钥`, "success");
            }

            // 重置页码和选中状态
            this.selectedKeys = [];
            this.keyPage = 1;
        },

        removeGroupApiKey(index) {
            if (this.groupFormData.api_keys.length > 1) {
                this.groupFormData.api_keys.splice(index, 1);
                // 调整页码如果当前页没有数据了
                if (
                    this.keyPageOffset >=
                    this.groupFormData.api_keys.length &&
                    this.keyPage > 1
                ) {
                    this.keyPage--;
                }
                // 清除选中状态
                this.selectedKeys = this.selectedKeys.filter(
                    (i) => i < this.groupFormData.api_keys.length,
                );
            }
        },

        // 批量添加密钥
        addBatchKeys() {
            const keys = this.batchKeysText
                .split("\n")
                .map((line) => line.trim())
                .filter((line) => line.length > 0);

            if (keys.length > 0) {
                // 获取现有的非空密钥
                const existingKeys = this.groupFormData.api_keys
                    .map(key => key.trim())
                    .filter(key => key.length > 0);

                // 检查重复密钥
                const duplicateKeys = [];
                const newUniqueKeys = [];

                keys.forEach(key => {
                    if (existingKeys.includes(key)) {
                        duplicateKeys.push(key);
                    } else if (!newUniqueKeys.includes(key)) {
                        newUniqueKeys.push(key);
                    }
                });

                // 添加新的唯一密钥
                if (newUniqueKeys.length > 0) {
                    this.groupFormData.api_keys.push(...newUniqueKeys);
                }

                this.batchKeysText = "";
                this.showBatchAddModal = false;

                // 显示结果消息
                let message = `成功添加 ${newUniqueKeys.length} 个密钥`;
                if (duplicateKeys.length > 0) {
                    message += `，跳过 ${duplicateKeys.length} 个重复密钥`;
                }
                this.showMessage(message, "success");
            }
        },

        // 删除选中的密钥
        deleteSelectedKeys() {
            if (this.selectedKeys.length === 0) return;

            if (
                !confirm(
                    `确定要删除选中的 ${this.selectedKeys.length} 个密钥吗？`,
                )
            ) {
                return;
            }

            // 按索引倒序删除，避免索引变化问题
            const sortedIndexes = [...this.selectedKeys].sort(
                (a, b) => b - a,
            );
            sortedIndexes.forEach((index) => {
                this.groupFormData.api_keys.splice(index, 1);
            });

            // 确保至少有一个空项
            if (this.groupFormData.api_keys.length === 0) {
                this.groupFormData.api_keys.push("");
            }

            // 清除选中状态
            this.selectedKeys = [];

            // 调整页码
            if (
                this.keyPageOffset >=
                this.groupFormData.api_keys.length &&
                this.keyPage > 1
            ) {
                this.keyPage--;
            }

            this.showMessage(
                `成功删除 ${sortedIndexes.length} 个密钥`,
                "success",
            );
        },

        // 删除失效密钥
        deleteInvalidKeys() {
            // 找出所有失效的密钥索引
            const invalidIndexes = [];
            this.groupFormData.api_keys.forEach((key, index) => {
                if (
                    key.trim() &&
                    this.keyValidationStatus[index] === "invalid"
                ) {
                    invalidIndexes.push(index);
                }
            });

            if (invalidIndexes.length === 0) {
                this.showMessage("没有找到失效的密钥", "info");
                return;
            }

            if (
                !confirm(
                    `确定要删除 ${invalidIndexes.length} 个失效密钥吗？`,
                )
            ) {
                return;
            }

            // 按索引倒序删除
            const sortedIndexes = [...invalidIndexes].sort(
                (a, b) => b - a,
            );
            sortedIndexes.forEach((index) => {
                this.groupFormData.api_keys.splice(index, 1);
            });

            // 确保至少有一个空项
            if (this.groupFormData.api_keys.length === 0) {
                this.groupFormData.api_keys.push("");
            }

            // 重新构建验证状态，移除已删除密钥的状态
            const newValidationStatus = {};
            const newInvalidIndexes = [];
            this.groupFormData.api_keys.forEach((key, newIndex) => {
                // 找到原来的索引对应的状态
                for (
                    let oldIndex = 0;
                    oldIndex <
                    this.groupFormData.api_keys.length +
                    sortedIndexes.length;
                    oldIndex++
                ) {
                    if (
                        !sortedIndexes.includes(oldIndex) &&
                        this.keyValidationStatus[oldIndex]
                    ) {
                        const adjustedIndex =
                            oldIndex -
                            sortedIndexes.filter(
                                (idx) => idx < oldIndex,
                            ).length;
                        if (adjustedIndex === newIndex) {
                            newValidationStatus[newIndex] =
                                this.keyValidationStatus[oldIndex];
                            if (
                                this.keyValidationStatus[
                                oldIndex
                                ] === "invalid"
                            ) {
                                newInvalidIndexes.push(newIndex);
                            }
                            break;
                        }
                    }
                }
            });

            this.keyValidationStatus = newValidationStatus;
            this.invalidKeyIndexes = newInvalidIndexes;

            // 调整页码
            if (
                this.keyPageOffset >=
                this.groupFormData.api_keys.length &&
                this.keyPage > 1
            ) {
                this.keyPage--;
            }

            this.showMessage(
                `成功删除 ${sortedIndexes.length} 个失效密钥`,
                "success",
            );
        },

        // 验证密钥
        async validateKeys() {
            const validKeys = this.groupFormData.api_keys.filter(
                (key) => key.trim().length > 0,
            );
            if (validKeys.length === 0) {
                this.showMessage(
                    "请先添加至少一个API密钥",
                    "error",
                );
                return;
            }

            if (
                !this.groupFormData.provider_type ||
                !this.groupFormData.base_url
            ) {
                this.showMessage(
                    "请先填写服务商类型和Base URL",
                    "error",
                );
                return;
            }

            this.validatingKeys = true;
            this.keyValidationStatus = {};
            this.invalidKeyIndexes = [];

            try {
                // 标记所有密钥为验证中
                this.groupFormData.api_keys.forEach(
                    (key, index) => {
                        if (key.trim().length > 0) {
                            this.keyValidationStatus[index] =
                                "validating";
                        }
                    },
                );

                // 创建临时分组配置来验证密钥
                const tempGroupData = {
                    name: this.groupFormData.name || "temp",
                    provider_type: this.groupFormData.provider_type,
                    base_url: this.groupFormData.base_url,
                    enabled: true,
                    timeout: this.groupFormData.timeout || 30,
                    max_retries:
                        this.groupFormData.max_retries || 3,
                    rotation_strategy:
                        this.groupFormData.rotation_strategy ||
                        "round_robin",
                    api_keys: validKeys,
                };

                const response = await fetch(
                    "/admin/keys/validate",
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify(tempGroupData),
                    },
                );

                if (response.ok) {
                    const data = await response.json();
                    if (data.success && data.results) {
                        let validCount = 0;
                        let invalidCount = 0;

                        // 更新验证状态
                        data.results.forEach(
                            (result, resultIndex) => {
                                // 找到对应的原始索引
                                let originalIndex = 0;
                                let validKeyIndex = 0;
                                for (
                                    let i = 0;
                                    i <
                                    this.groupFormData.api_keys
                                        .length;
                                    i++
                                ) {
                                    if (
                                        this.groupFormData.api_keys[
                                            i
                                        ].trim().length > 0
                                    ) {
                                        if (
                                            validKeyIndex ===
                                            resultIndex
                                        ) {
                                            originalIndex = i;
                                            break;
                                        }
                                        validKeyIndex++;
                                    }
                                }

                                if (result.valid) {
                                    this.keyValidationStatus[
                                        originalIndex
                                    ] = "valid";
                                    validCount++;
                                } else {
                                    this.keyValidationStatus[
                                        originalIndex
                                    ] = "invalid";
                                    this.invalidKeyIndexes.push(
                                        originalIndex,
                                    );
                                    invalidCount++;
                                }
                            },
                        );

                        this.showMessage(
                            `验证完成：${validCount} 个有效，${invalidCount} 个无效`,
                            validCount > 0 ? "success" : "error",
                        );
                    } else {
                        this.showMessage(
                            "验证失败: " +
                            (data.message || "未知错误"),
                            "error",
                        );
                    }
                } else {
                    const errorData = await response.json();
                    this.showMessage(
                        "验证失败: " +
                        (errorData.message || "网络错误"),
                        "error",
                    );
                }
            } catch (error) {
                this.showMessage(
                    "验证失败: " + error.message,
                    "error",
                );
            } finally {
                this.validatingKeys = false;
            }
        },

        // 清空所有密钥
        clearAllKeys() {
            if (this.groupFormData.api_keys.filter(k => k.trim()).length === 0) {
                this.showMessage("没有密钥需要清空", "info");
                return;
            }

            if (!confirm("确定要清空所有密钥吗？此操作不可恢复。")) {
                return;
            }

            // 清空所有密钥，只保留一个空项
            this.groupFormData.api_keys = [""];

            // 清除选中状态和验证状态
            this.selectedKeys = [];
            this.keyValidationStatus = {};
            this.invalidKeyIndexes = [];

            // 重置页码
            this.keyPage = 1;

            this.showMessage("已清空所有密钥", "success");
        },

        // 导出密钥
        exportKeys() {
            const validKeys = this.groupFormData.api_keys.filter(k => k.trim());

            if (validKeys.length === 0) {
                this.showMessage("没有密钥可以导出", "info");
                return;
            }

            try {
                // 创建文件内容，每行一个密钥
                const content = validKeys.join('\n');

                // 生成文件名
                const groupName = this.groupFormData.name || this.groupFormData.group_id || 'group';
                const timestamp = new Date().toISOString().replace(/[:.]/g, '-').split('T')[0];
                const filename = `${groupName}_api_keys_${timestamp}.txt`;

                // 创建并下载文件
                const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = filename;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);

                this.showMessage(`已导出 ${validKeys.length} 个密钥到文件 ${filename}`, "success");
            } catch (error) {
                console.error('导出密钥失败:', error);
                this.showMessage("导出密钥失败: " + error.message, "error");
            }
        },

        // 强制设置密钥状态
        async forceSetKeyStatus(keyIndex, status) {
            const apiKey = this.groupFormData.api_keys[keyIndex];
            if (!apiKey || !apiKey.trim()) {
                this.showMessage("密钥不能为空", "error");
                return;
            }

            if (!this.editingGroupId) {
                this.showMessage("只能在编辑模式下强制设置密钥状态", "error");
                return;
            }

            // 设置加载状态
            this.forcingKeyStatus[keyIndex] = true;
            this.forcingKeyStatus = { ...this.forcingKeyStatus };

            try {
                const response = await fetch(
                    `/admin/groups/${this.editingGroupId}/keys/force-status`,
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify({
                            api_key: apiKey.trim(),
                            status: status,
                        }),
                    }
                );

                const result = await response.json();

                if (response.ok && result.success) {
                    // 更新本地验证状态
                    this.keyValidationStatus[keyIndex] = status;
                    this.keyValidationStatus = { ...this.keyValidationStatus };

                    // 更新失效密钥索引列表
                    if (status === 'invalid') {
                        if (!this.invalidKeyIndexes.includes(keyIndex)) {
                            this.invalidKeyIndexes.push(keyIndex);
                        }
                    } else {
                        const invalidIndex = this.invalidKeyIndexes.indexOf(keyIndex);
                        if (invalidIndex > -1) {
                            this.invalidKeyIndexes.splice(invalidIndex, 1);
                        }
                    }

                    // 刷新服务商状态列表以更新分组列表中的信息
                    await this.loadProviderStatuses();

                    // 重新加载持久化验证状态以更新密钥状态统计
                    await this.loadPersistedValidationStatus(false);

                    this.showMessage(
                        `密钥状态已强制设置为${status === 'valid' ? '有效' : '无效'}`,
                        "success"
                    );
                } else {
                    throw new Error(result.message || "设置密钥状态失败");
                }
            } catch (error) {
                console.error("强制设置密钥状态失败:", error);
                this.showMessage(
                    "强制设置密钥状态失败: " + error.message,
                    "error"
                );
            } finally {
                // 清除加载状态
                this.forcingKeyStatus[keyIndex] = false;
                this.forcingKeyStatus = { ...this.forcingKeyStatus };
            }
        },

        // 一键删除失效密钥（从服务器）
        async bulkDeleteInvalidKeysFromServer() {
            if (!this.editingGroupId) {
                this.showMessage("只能在编辑模式下删除失效密钥", "error");
                return;
            }

            // 检查本地是否有失效密钥
            const localInvalidCount = this.getInvalidKeyCount();

            // 如果本地没有失效密钥，但分组列表显示有失效密钥，提示用户先检测
            if (localInvalidCount === 0) {
                const groupKeyStatus = this.keyStatus.groups && this.keyStatus.groups[this.editingGroupId];
                if (groupKeyStatus && groupKeyStatus.invalid_keys > 0) {
                    this.showMessage(
                        `检测到 ${groupKeyStatus.invalid_keys} 个失效密钥，但本地验证状态未加载。请先点击"验证密钥"按钮检测密钥状态，然后再执行删除操作。`,
                        "error"
                    );
                    return;
                } else {
                    this.showMessage("没有失效密钥需要删除", "info");
                    return;
                }
            }

            if (!confirm(`确定要删除 ${localInvalidCount} 个失效密钥吗？此操作不可恢复。`)) {
                return;
            }

            this.bulkDeletingInvalidKeys = true;

            try {
                const response = await fetch(
                    `/admin/groups/${this.editingGroupId}/keys/invalid`,
                    {
                        method: "DELETE",
                    }
                );

                const result = await response.json();

                if (response.ok && result.success) {
                    // 从本地数组中移除失效密钥
                    const validKeys = [];
                    this.groupFormData.api_keys.forEach((key, index) => {
                        if (key.trim() && this.keyValidationStatus[index] !== 'invalid') {
                            validKeys.push(key);
                        } else if (!key.trim()) {
                            // 保留空密钥项
                            validKeys.push(key);
                        }
                    });

                    // 如果所有密钥都被删除了，至少保留一个空项
                    if (validKeys.filter(k => k.trim()).length === 0) {
                        validKeys.push("");
                    }

                    this.groupFormData.api_keys = validKeys;

                    // 重建验证状态
                    const newValidationStatus = {};
                    const newInvalidIndexes = [];
                    this.groupFormData.api_keys.forEach((key, newIndex) => {
                        // 只保留有效密钥的状态
                        if (key.trim()) {
                            newValidationStatus[newIndex] = 'valid'; // 假设剩余的都是有效的
                        }
                    });

                    this.keyValidationStatus = newValidationStatus;
                    this.invalidKeyIndexes = newInvalidIndexes;

                    // 重置页码和选中状态
                    this.selectedKeys = [];
                    this.keyPage = 1;

                    // 刷新服务商状态列表以更新分组列表中的信息
                    await this.loadProviderStatuses();

                    // 重新加载持久化验证状态以更新密钥状态统计
                    await this.loadPersistedValidationStatus(false);

                    this.showMessage(
                        `成功删除 ${result.deleted_count} 个失效密钥`,
                        "success"
                    );
                } else {
                    throw new Error(result.message || "删除失效密钥失败");
                }
            } catch (error) {
                console.error("一键删除失效密钥失败:", error);
                this.showMessage(
                    "一键删除失效密钥失败: " + error.message,
                    "error"
                );
            } finally {
                this.bulkDeletingInvalidKeys = false;
            }
        },

        // 获取API密钥验证状态
        async loadKeyValidationStatus(groupId) {
            if (!groupId) return;

            try {
                const response = await fetch(
                    `/admin/keys/validation/${groupId}`,
                );
                if (response.ok) {
                    const data = await response.json();
                    if (data.success && data.validation_status) {
                        // 更新密钥验证状态
                        this.keyValidationStatus = {};
                        this.invalidKeyIndexes = [];
                        this.groupFormData.api_keys.forEach(
                            (key, index) => {
                                if (
                                    key.trim() &&
                                    data.validation_status[
                                    key.trim()
                                    ]
                                ) {
                                    const status =
                                        data.validation_status[
                                        key.trim()
                                        ];
                                    if (status.is_valid === true) {
                                        this.keyValidationStatus[
                                            index
                                        ] = "valid";
                                    } else if (
                                        status.is_valid === false
                                    ) {
                                        this.keyValidationStatus[
                                            index
                                        ] = "invalid";
                                        this.invalidKeyIndexes.push(
                                            index,
                                        );
                                    }
                                }
                            },
                        );
                    }
                }
            } catch (error) {
                console.error(
                    "Failed to load key validation status:",
                    error,
                );
            }
        },

        // 获取密钥验证样式类
        getKeyValidationClass(index) {
            const status = this.keyValidationStatus[index];
            if (status === "valid") {
                return "bg-green-50 border-green-200";
            } else if (status === "invalid") {
                return "bg-red-50 border-red-200";
            } else if (status === "validating") {
                return "bg-yellow-50 border-yellow-200";
            }
            return "";
        },

        // 获取失效密钥数量
        getInvalidKeyCount() {
            let count = 0;
            this.groupFormData.api_keys.forEach((key, index) => {
                if (
                    key.trim() &&
                    this.keyValidationStatus[index] === "invalid"
                ) {
                    count++;
                }
            });
            return count;
        },

        // 加载可用模型
        async loadAvailableModels() {
            // 如果是编辑模式，可以直接从现有分组加载模型
            if (this.showEditGroupModal && this.editingGroupId) {
                this.loadingModels = true;
                try {
                    const response = await fetch(
                        `/admin/models/available/${this.editingGroupId}`,
                    );

                    if (response.ok) {
                        const data = await response.json();
                        // 解析响应数据
                        const groupData =
                            data.data[this.editingGroupId];
                        if (
                            groupData &&
                            groupData.models &&
                            groupData.models.data
                        ) {
                            this.availableModels =
                                groupData.models.data;
                            this.filterModels();
                            this.showMessage(
                                `成功加载 ${this.availableModels.length} 个模型`,
                                "success",
                            );
                        } else {
                            this.availableModels = [];
                            this.filterModels();
                            this.showMessage(
                                "未找到可用模型",
                                "error",
                            );
                        }
                    } else {
                        // API调用失败时清空可用模型列表
                        this.availableModels = [];
                        this.filterModels();
                    const errorData = await response.json();
                    const errMsg = (errorData && (
                        (typeof errorData.error === 'string' && errorData.error) ||
                        (errorData.error && errorData.error.message) ||
                        errorData.message
                    )) || "未知错误";
                    this.showMessage(
                        "加载模型失败: " + errMsg,
                        "error",
                    );
                    }
                } catch (error) {
                    // 网络错误时也清空可用模型列表
                    this.availableModels = [];
                    this.filterModels();
                    this.showMessage(
                        "网络错误: " + error.message,
                        "error",
                    );
                } finally {
                    this.loadingModels = false;
                }
                return;
            }

            // 创建模式需要先验证配置
            if (
                !this.groupFormData.provider_type ||
                !this.groupFormData.base_url
            ) {
                this.showMessage(
                    "请先填写服务商类型和Base URL",
                    "error",
                );
                return;
            }

            const validKeys = this.groupFormData.api_keys.filter(
                (key) => key.trim().length > 0,
            );
            if (validKeys.length === 0) {
                this.showMessage(
                    "请先添加至少一个API密钥",
                    "error",
                );
                return;
            }

            this.loadingModels = true;
            try {
                // 在获取模型之前，先解析JSON请求头（如果有的话）
                let parsedHeaders = {};
                if (this.headersText && this.headersText.trim()) {
                    try {
                        parsedHeaders = JSON.parse(this.headersText.trim());
                    } catch (e) {
                        this.showMessage(
                            "JSON请求头格式错误: " + e.message,
                            "error",
                        );
                        this.loadingModels = false;
                        return;
                    }
                }

                // 创建临时分组配置来测试模型加载
                const tempGroupData = {
                    name: this.groupFormData.name || "temp",
                    provider_type: this.groupFormData.provider_type,
                    base_url: this.groupFormData.base_url,
                    enabled: true,
                    timeout: this.groupFormData.timeout || 30,
                    max_retries:
                        this.groupFormData.max_retries || 3,
                    rotation_strategy:
                        this.groupFormData.rotation_strategy ||
                        "round_robin",
                    api_keys: validKeys,
                };

                // 使用新的按类型加载模型API
                const requestData = {
                    provider_type: tempGroupData.provider_type,
                    base_url: tempGroupData.base_url,
                    api_keys: tempGroupData.api_keys,
                    timeout_seconds: tempGroupData.timeout,
                    max_retries: tempGroupData.max_retries,
                    headers: parsedHeaders,
                };

                const response = await fetch(
                    "/admin/models/available/by-type",
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify(requestData),
                    },
                );

                if (response.ok) {
                    const data = await response.json();
                    // 解析新API的响应格式
                    const groupData = data.data["temp-group"];
                    if (
                        groupData &&
                        groupData.models &&
                        groupData.models.data
                    ) {
                        this.availableModels =
                            groupData.models.data;
                        this.filterModels();

                        // 如果modelsText有内容，同步到groupFormData.models以便自动勾选
                        if (this.modelsText && this.modelsText.trim()) {
                            this.syncTextToModels();
                        }

                        this.showMessage(
                            `成功加载 ${this.availableModels.length} 个模型`,
                            "success",
                        );
                    } else {
                        this.availableModels = [];
                        this.filterModels();
                        this.showMessage("未找到可用模型", "error");
                    }
                } else {
                    // API调用失败时清空可用模型列表
                    this.availableModels = [];
                    this.filterModels();
                    const errorData = await response.json();
                    this.showMessage(
                        "加载模型失败: " +
                        (errorData.error || "未知错误"),
                        "error",
                    );
                }
            } catch (error) {
                // 网络错误时也清空可用模型列表
                this.availableModels = [];
                this.filterModels();
                this.showMessage(
                    "网络错误: " + error.message,
                    "error",
                );
            } finally {
                this.loadingModels = false;
            }
        },

        // 加载模型并自动选择（基于系统中已存在的模型）
        async loadAvailableModelsAndAutoSelect() {
            this.showMessage("开始加载模型并自动选择...", "info");

            // 设置加载状态
            this.loadingModels = true;

            try {
                // 先调用原有的加载模型逻辑
                await this.loadAvailableModels();


                if (this.availableModels.length === 0) {
                    this.showMessage("没有加载到任何可用模型", "warning");
                    return;
                }
                // 获取两组独立数据：系统模型数据 + 历史别名映射数据
                const [groupsResponse, aliasesResponse] = await Promise.all([
                    fetch('/admin/groups/manage'),  // 系统模型数据
                    fetch('/admin/models/all-aliases')  // 历史别名映射数据
                ]);

                if (!groupsResponse.ok || !aliasesResponse.ok) {
                    this.showMessage("获取系统数据失败", "error");
                    return;
                }

                const groupsData = await groupsResponse.json();
                const historicalAliasesData = await aliasesResponse.json();
                

                // 收集所有分组中已配置的实际模型名称（去重）
                const existingModels = new Set();

                if (groupsData.success && groupsData.groups) {
                    const groupEntries = Object.entries(groupsData.groups);
                    let enabledGroupCount = 0;
                    let groupsWithModels = 0;

                    groupEntries.forEach(([groupId, group]) => {

                        if (group.enabled) {
                            enabledGroupCount++;
                            if (group.models && Array.isArray(group.models) && group.models.length > 0) {
                                groupsWithModels++;
                                group.models.forEach(model => {
                                    if (model && model.trim()) {
                                        existingModels.add(model.trim());
                                    }
                                });
                            } else {
                                console.warn(`启用的分组 ${group.group_name} 没有模型配置`);
                            }
                        }
                    });

                }


                // 获取当前分组的别名映射（用于匹配）
                const currentAliasMapping = new Map(); // key: 别名, value: [实际模型名数组]
                if (groupsData.success && groupsData.groups) {
                    let groupsWithAliases = 0;
                    Object.entries(groupsData.groups).forEach(([groupId, group]) => {
                        if (group.enabled) {
                            if (group.model_aliases && typeof group.model_aliases === 'object' && Object.keys(group.model_aliases).length > 0) {
                                groupsWithAliases++;
                                Object.entries(group.model_aliases).forEach(([alias, actualModel]) => {
                                    if (alias && actualModel && alias.trim() && actualModel.trim()) {
                                        const aliasKey = alias.trim();
                                        const modelValue = actualModel.trim();
                                        
                                        // 合并映射：一个别名可以指向多个模型
                                        if (!currentAliasMapping.has(aliasKey)) {
                                            currentAliasMapping.set(aliasKey, []);
                                        }
                                        const existingModels = currentAliasMapping.get(aliasKey);
                                        if (!existingModels.includes(modelValue)) {
                                            existingModels.push(modelValue);
                                        }
                                    }
                                });
                            }
                        }
                    });
                }

                // 将历史别名数据也添加到当前别名映射中（用于匹配）
                if (historicalAliasesData.success && historicalAliasesData.aliases) {
                    historicalAliasesData.aliases.forEach(aliasInfo => {
                        const actualModel = aliasInfo.actual_model;
                        const alias = aliasInfo.alias;
                        
                        if (actualModel && alias && alias.trim() && actualModel.trim()) {
                            const aliasKey = alias.trim();
                            const modelValue = actualModel.trim();
                            
                            // 合并历史别名映射
                            if (!currentAliasMapping.has(aliasKey)) {
                                currentAliasMapping.set(aliasKey, []);
                            }
                            const existingModels = currentAliasMapping.get(aliasKey);
                            if (!existingModels.includes(modelValue)) {
                                existingModels.push(modelValue);
                            }
                        }
                    });
                }

                // 构建历史别名映射数据（用于第二阶段添加额外别名）
                const historicalAliasMapping = new Map(); // key: 实际模型名, value: [别名数组]
                if (historicalAliasesData.success && historicalAliasesData.aliases) {
                    historicalAliasesData.aliases.forEach(aliasInfo => {
                        const actualModel = aliasInfo.actual_model;
                        const alias = aliasInfo.alias;
                        
                        if (actualModel && alias) {
                            if (!historicalAliasMapping.has(actualModel)) {
                                historicalAliasMapping.set(actualModel, []);
                            }
                            historicalAliasMapping.get(actualModel).push(alias);
                        }
                    });
                }

                // 自动选择匹配的模型（恢复原有逻辑）
                let autoSelectedCount = 0;
                const matchDetails = [];
                const selectedModels = []; // 记录选中的模型

                this.availableModels.forEach(model => {
                    const modelId = model.id;
                    let shouldSelect = false;
                    let matchReason = '';

                    // 1. 检查是否在已存在的模型列表中（直接匹配）
                    if (existingModels.has(modelId)) {
                        shouldSelect = true;
                        matchReason = '直接匹配已存在模型';
                    }

                    // 2. 检查是否有别名映射指向这个模型（无论是否已经匹配）
                    let foundAliases = [];
                    for (const [alias, actualModels] of currentAliasMapping) {
                        if (actualModels.includes(modelId)) {
                            foundAliases.push(`${alias} -> ${modelId}`);
                            if (!shouldSelect) {
                                shouldSelect = true;
                                matchReason = `通过别名 "${alias}" 匹配`;
                            }
                            // 无论如何都要设置别名映射
                            if (!this.groupFormData.model_aliases) {
                                this.groupFormData.model_aliases = {};
                            }
                            this.groupFormData.model_aliases[alias] = modelId;
                        }
                    }
                    if (foundAliases.length > 0) {
                    }

                    // 3. 检查当前模型是否本身就是一个别名
                    if (!shouldSelect && currentAliasMapping.has(modelId)) {
                        const actualModels = currentAliasMapping.get(modelId);
                        // 检查这些实际模型是否有任何一个在系统中存在
                        for (const actualModel of actualModels) {
                            if (existingModels.has(actualModel)) {
                                shouldSelect = true;
                                matchReason = `作为别名匹配到实际模型 "${actualModel}"`;
                                // 设置别名映射
                                if (!this.groupFormData.model_aliases) {
                                    this.groupFormData.model_aliases = {};
                                }
                                this.groupFormData.model_aliases[modelId] = actualModel;
                                break; // 找到第一个有效的实际模型就停止
                            }
                        }
                    }

                    if (shouldSelect && !this.groupFormData.models.includes(modelId)) {
                        this.groupFormData.models.push(modelId);
                        selectedModels.push(modelId);
                        autoSelectedCount++;
                        matchDetails.push(`${modelId} (${matchReason})`);
                    } else if (shouldSelect) {
                        selectedModels.push(modelId); // 仍然记录为选中，用于后续历史别名检查
                    }
                });

                // 第二阶段：为选中的模型检查并添加历史别名映射
                let aliasAddedCount = 0;
                const aliasDetails = [];

                selectedModels.forEach(modelId => {
                    // 检查该模型是否在历史别名映射中存在
                    if (historicalAliasMapping.has(modelId)) {
                        const aliases = historicalAliasMapping.get(modelId);
                        
                        // 初始化别名映射对象
                        if (!this.groupFormData.model_aliases) {
                            this.groupFormData.model_aliases = {};
                        }

                        // 添加所有历史别名映射
                        aliases.forEach(alias => {
                            this.groupFormData.model_aliases[alias] = modelId;
                            aliasAddedCount++;
                            aliasDetails.push(`${alias} -> ${modelId}`);
                        });
                    }
                });

                // 同步到文本框
                this.syncModelsToText();
                this.syncAliasesToText();

                // 生成结果消息
                let resultMessage = '';
                if (autoSelectedCount > 0) {
                    resultMessage = `自动选择了 ${autoSelectedCount} 个模型`;
                    if (aliasAddedCount > 0) {
                        resultMessage += `，并添加了 ${aliasAddedCount} 个历史别名映射`;
                    }
                    this.showMessage(resultMessage, "success");
                } else {
                    this.showMessage(
                        `没有找到匹配的模型。可用模型 ${this.availableModels.length} 个，系统模型 ${existingModels.size} 个，请检查模型名称是否一致`,
                        "warning"
                    );
                }

            } catch (error) {
                this.showMessage(`自动选择模型时出错: ${error.message}`, "error");
            } finally {
                // 确保加载状态被重置
                this.loadingModels = false;
            }
        },

        // 筛选模型
        filterModels() {
            if (!this.modelSearchQuery.trim()) {
                this.filteredModels = this.availableModels;
            } else {
                const query = this.modelSearchQuery.toLowerCase();
                this.filteredModels = this.availableModels.filter(
                    (model) =>
                        model.id.toLowerCase().includes(query) ||
                        (model.owned_by &&
                            model.owned_by
                                .toLowerCase()
                                .includes(query)),
                );
            }
        },

        // 清除模型搜索
        clearModelSearch() {
            this.modelSearchQuery = "";
            this.filterModels();
        },

        // 全选模型
        selectAllModels() {
            this.groupFormData.models = this.filteredModels.map(
                (model) => model.id,
            );
        },

        // 清空模型选择
        clearAllModels() {
            this.groupFormData.models = [];
            this.modelsText = "";
        },

        // 移除单个模型
        removeModel(modelId) {
            this.groupFormData.models =
                this.groupFormData.models.filter(
                    (id) => id !== modelId,
                );
            // 同步到文本框
            this.syncModelsToText();
        },

        // 将预选模型列表同步到文本框
        syncModelsToText() {
            this.modelsText = this.groupFormData.models.join('\n');
        },

        // 将自动匹配的别名映射同步到模型映射列表
        syncAliasesToText() {
            if (this.groupFormData.model_aliases && Object.keys(this.groupFormData.model_aliases).length > 0) {
                // 将 model_aliases 对象转换为 modelMappings 数组格式
                for (const [alias, originalModel] of Object.entries(this.groupFormData.model_aliases)) {
                    // 检查是否已经存在相同的映射，避免重复添加
                    const existingMapping = this.modelMappings.find(mapping => 
                        mapping.alias === alias && mapping.original === originalModel
                    );
                    
                    if (!existingMapping) {
                        this.modelMappings.push({
                            alias: alias,
                            original: originalModel
                        });
                    }
                }
                
            }
        },

        // 将文本框内容同步到预选模型列表
        syncTextToModels() {
            const textModels = this.modelsText
                .split('\n')
                .map(line => line.trim())
                .filter(line => line.length > 0);
            this.groupFormData.models = [...new Set(textModels)];
        },

        // 验证JSON请求参数格式
        validateRequestParams() {
            if (!this.requestParamsText.trim()) {
                this.requestParamsValidationMessage = "";
                this.requestParamsValidationError = false;
                return;
            }

            try {
                const parsed = JSON.parse(
                    this.requestParamsText.trim(),
                );

                // 检查是否是对象
                if (
                    typeof parsed !== "object" ||
                    Array.isArray(parsed) ||
                    parsed === null
                ) {
                    this.requestParamsValidationMessage =
                        "JSON必须是一个对象";
                    this.requestParamsValidationError = true;
                    return;
                }

                // 检查支持的参数
                const supportedParams = [
                    "temperature",
                    "max_tokens",
                    "top_p",
                    "stop",
                ];
                const unsupportedParams = Object.keys(
                    parsed,
                ).filter((key) => !supportedParams.includes(key));

                if (unsupportedParams.length > 0) {
                    this.requestParamsValidationMessage = `不支持的参数: ${unsupportedParams.join(", ")}。支持的参数: ${supportedParams.join(", ")}`;
                    this.requestParamsValidationError = true;
                    return;
                }

                // 验证参数值类型
                if (
                    "temperature" in parsed &&
                    (typeof parsed.temperature !== "number" ||
                        parsed.temperature < 0 ||
                        parsed.temperature > 2)
                ) {
                    this.requestParamsValidationMessage =
                        "temperature必须是0-2之间的数字";
                    this.requestParamsValidationError = true;
                    return;
                }

                if (
                    "max_tokens" in parsed &&
                    (!Number.isInteger(parsed.max_tokens) ||
                        parsed.max_tokens <= 0)
                ) {
                    this.requestParamsValidationMessage =
                        "max_tokens必须是正整数";
                    this.requestParamsValidationError = true;
                    return;
                }

                if (
                    "top_p" in parsed &&
                    (typeof parsed.top_p !== "number" ||
                        parsed.top_p < 0 ||
                        parsed.top_p > 1)
                ) {
                    this.requestParamsValidationMessage =
                        "top_p必须是0-1之间的数字";
                    this.requestParamsValidationError = true;
                    return;
                }

                if (
                    "stop" in parsed &&
                    !Array.isArray(parsed.stop)
                ) {
                    this.requestParamsValidationMessage =
                        "stop必须是字符串数组";
                    this.requestParamsValidationError = true;
                    return;
                }

                this.requestParamsValidationMessage =
                    "JSON格式正确";
                this.requestParamsValidationError = false;
            } catch (e) {
                this.requestParamsValidationMessage =
                    "JSON格式错误: " + e.message;
                this.requestParamsValidationError = true;
            }
        },
        // 验证JSON请求头格式
        validateHeaders() {
            if (!this.headersText.trim()) {
                this.headersValidationMessage = "";
                this.headersValidationError = false;
                return;
            }
            try {
                const parsed = JSON.parse(
                    this.headersText.trim(),
                );
                // 检查是否是对象
                if (
                    typeof parsed !== "object" ||
                    Array.isArray(parsed) ||
                    parsed === null
                ) {
                    this.headersValidationMessage =
                        "JSON必须是一个对象";
                    this.headersValidationError = true;
                    return;
                }
                // 检查所有值是否为字符串
                for (const [key, value] of Object.entries(parsed)) {
                    if (typeof value !== "string") {
                        this.headersValidationMessage =
                            `请求头 "${key}" 的值必须是字符串`;
                        this.headersValidationError = true;
                        return;
                    }
                }
                this.headersValidationMessage =
                    "JSON格式正确";
                this.headersValidationError = false;
            } catch (e) {
                this.headersValidationMessage =
                    "JSON格式错误: " + e.message;
                this.headersValidationError = true;
            }
        },

        // 添加模型映射
        addModelMapping() {
            this.modelMappings.push({ alias: "", original: "" });
        },

        // 移除模型映射
        removeModelMapping(index) {
            this.modelMappings.splice(index, 1);
        },

        // 根据实际模型名称相似度从现有别名中自动选择
        suggestAliasFromSimilarity(mapping) {
            if (!mapping.original || mapping.alias || !this.allConfiguredAliases || this.allConfiguredAliases.length === 0) {
                return; // 如果没有选择模型、已经有别名、或没有可用的别名列表，不做推荐
            }

            // 预处理实际模型名称：移除 / 之前的内容，然后按 - 分割
            const processedModelName = this.preprocessModelName(mapping.original);
            
            let bestMatch = '';
            let highestSimilarity = 0;

            // 遍历所有已配置的别名，找到相似度最高的
            for (const alias of this.allConfiguredAliases) {
                // 同样预处理别名进行比较
                const processedAlias = this.preprocessModelName(alias);
                const similarity = this.calculateSimilarity(processedModelName, processedAlias);
                if (similarity > highestSimilarity && similarity > 0.3) { // 只有相似度超过30%才考虑
                    highestSimilarity = similarity;
                    bestMatch = alias;
                }
            }

            // 如果找到了相似度较高的别名，自动设置
            if (bestMatch && highestSimilarity > 0.3) {
                mapping.alias = bestMatch;
            }
        },

        // 预处理模型名称：移除/之前的内容，保留/之后的内容进行匹配
        preprocessModelName(modelName) {
            if (!modelName) return '';
            
            // 移除 / 之前的内容（如果包含 /）
            const afterSlash = modelName.includes('/') ? modelName.substring(modelName.lastIndexOf('/') + 1) : modelName;
            
            // 转换为小写并按 - 分割进行标准化
            return afterSlash.toLowerCase();
        },

        // 计算两个字符串的相似度（基于分段匹配和智能相似度）
        calculateSimilarity(str1, str2) {
            // 如果完全相同，返回1
            if (str1 === str2) return 1;

            // 按 - 分割成段
            const parts1 = str1.split('-').filter(part => part.length > 0);
            const parts2 = str2.split('-').filter(part => part.length > 0);

            if (parts1.length === 0 || parts2.length === 0) {
                return 0;
            }

            // 计算匹配的段数
            let matchedParts = 0;
            let totalWeight = 0;

            // 为每个段分配权重（前面的段权重更高）
            for (let i = 0; i < parts1.length; i++) {
                const part1 = parts1[i];
                const weight = Math.max(1, parts1.length - i); // 前面的段权重更高
                totalWeight += weight;

                // 检查是否在第二个字符串的段中找到匹配
                let bestMatch = 0;
                for (let j = 0; j < parts2.length; j++) {
                    const part2 = parts2[j];

                    // 完全匹配
                    if (part1 === part2) {
                        bestMatch = 1;
                        break;
                    }
                    
                    // 部分匹配：检查是否一个包含另一个
                    if (part1.includes(part2) || part2.includes(part1)) {
                        const minLen = Math.min(part1.length, part2.length);
                        const maxLen = Math.max(part1.length, part2.length);
                        bestMatch = Math.max(bestMatch, minLen / maxLen * 0.8); // 部分匹配给予80%权重
                    }
                    
                    // 编辑距离相似度匹配（用于版本号等）
                    if (part1.length > 2 && part2.length > 2) {
                        const editSimilarity = this.calculateEditDistanceSimilarity(part1, part2);
                        if (editSimilarity > 0.6) { // 编辑距离相似度超过60%
                            bestMatch = Math.max(bestMatch, editSimilarity * 0.7); // 编辑距离匹配给予70%权重
                        }
                    }
                }

                matchedParts += bestMatch * weight;
            }

            // 对长度差异进行惩罚
            const lengthPenalty = Math.abs(parts1.length - parts2.length) * 0.1;
            const similarity = matchedParts / totalWeight - lengthPenalty;

            return Math.max(0, Math.min(1, similarity));
        },
        
        // 计算编辑距离相似度
        calculateEditDistanceSimilarity(str1, str2) {
            const maxLen = Math.max(str1.length, str2.length);
            if (maxLen === 0) return 1;
            
            const distance = this.levenshteinDistance(str1, str2);
            return 1 - distance / maxLen;
        },
        
        // 计算莱文斯坦距离（编辑距离）
        levenshteinDistance(str1, str2) {
            const dp = Array(str1.length + 1).fill(null).map(() => Array(str2.length + 1).fill(0));
            
            for (let i = 0; i <= str1.length; i++) {
                dp[i][0] = i;
            }
            for (let j = 0; j <= str2.length; j++) {
                dp[0][j] = j;
            }
            
            for (let i = 1; i <= str1.length; i++) {
                for (let j = 1; j <= str2.length; j++) {
                    if (str1[i - 1] === str2[j - 1]) {
                        dp[i][j] = dp[i - 1][j - 1];
                    } else {
                        dp[i][j] = Math.min(
                            dp[i - 1][j] + 1,     // 删除
                            dp[i][j - 1] + 1,     // 插入
                            dp[i - 1][j - 1] + 1  // 替换
                        );
                    }
                }
            }
            
            return dp[str1.length][str2.length];
        },

        // 检查是否是重复的别名
        isDuplicateAlias(alias, currentIndex) {
            if (!alias || !alias.trim()) {
                return false;
            }

            const trimmedAlias = alias.trim();
            let count = 0;

            for (let i = 0; i < this.modelMappings.length; i++) {
                if (this.modelMappings[i].alias &&
                    this.modelMappings[i].alias.trim() === trimmedAlias) {
                    count++;
                    if (count > 1 && i !== currentIndex) {
                        return true;
                    }
                }
            }

            return false;
        },

        // 获取重复别名的CSS类
        getDuplicateAliasClass(alias, currentIndex) {
            const baseClass = "w-full px-2 py-1 text-sm border rounded focus:outline-none focus:ring-1";

            if (this.isDuplicateAlias(alias, currentIndex)) {
                return baseClass + " border-orange-300 focus:ring-orange-500 bg-orange-50";
            } else {
                return baseClass + " border-gray-300 focus:ring-blue-500";
            }
        },

        // 验证粘贴的模型映射JSON
        validatePasteModelMapping() {
            if (!this.pasteModelMappingText.trim()) {
                this.pasteModelMappingValidationMessage = '';
                this.pasteModelMappingValidationError = false;
                return;
            }

            try {
                const parsed = JSON.parse(this.pasteModelMappingText.trim());

                if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
                    this.pasteModelMappingValidationMessage = '请输入有效的JSON对象格式';
                    this.pasteModelMappingValidationError = true;
                    return;
                }

                const entries = Object.entries(parsed);
                if (entries.length === 0) {
                    this.pasteModelMappingValidationMessage = 'JSON对象不能为空';
                    this.pasteModelMappingValidationError = true;
                    return;
                }

                // 验证每个键值对
                for (const [key, value] of entries) {
                    if (typeof key !== 'string' || key.trim() === '') {
                        this.pasteModelMappingValidationMessage = '所有键必须是非空字符串';
                        this.pasteModelMappingValidationError = true;
                        return;
                    }
                    if (typeof value !== 'string' || value.trim() === '') {
                        this.pasteModelMappingValidationMessage = '所有值必须是非空字符串';
                        this.pasteModelMappingValidationError = true;
                        return;
                    }
                }

                this.pasteModelMappingValidationMessage = `JSON格式正确，将添加 ${entries.length} 个模型映射`;
                this.pasteModelMappingValidationError = false;
            } catch (error) {
                this.pasteModelMappingValidationMessage = `JSON格式错误: ${error.message}`;
                this.pasteModelMappingValidationError = true;
            }
        },

        // 关闭粘贴模型映射弹窗
        closePasteModelMappingModal() {
            this.showPasteModelMappingModal = false;
            this.pasteModelMappingText = '';
            this.pasteModelMappingValidationMessage = '';
            this.pasteModelMappingValidationError = false;
        },

        // 应用粘贴的模型映射
        applyPasteModelMapping() {
            if (this.pasteModelMappingValidationError || !this.pasteModelMappingText.trim()) {
                return;
            }

            try {
                const parsed = JSON.parse(this.pasteModelMappingText.trim());
                const entries = Object.entries(parsed);

                // 添加到现有的模型映射列表中
                for (const [alias, original] of entries) {
                    this.modelMappings.push({
                        alias: alias.trim(),
                        original: original.trim()
                    });
                }

                // 关闭弹窗
                this.closePasteModelMappingModal();

                // 显示成功消息
                this.showMessage(`成功添加 ${entries.length} 个模型映射配置`, 'success');

            } catch (error) {
                this.showMessage(`解析JSON失败: ${error.message}`, 'error');
            }
        },

        showMessage(msg, type = "success") {
            this.message = msg;
            this.messageType = type;
            setTimeout(() => {
                this.message = "";
            }, 3000);
        },

        getKeyStatusText(groupId, provider) {
            const keyStatusData = this.keyStatus.groups[groupId];
            if (keyStatusData) {
                return `${keyStatusData.valid_keys}/${keyStatusData.total_keys}`;
            }
            return `${provider.available_keys || 0}/${provider.total_keys || 0}`;
        },

        // 获取详细的密钥状态数据
        getKeyStatusData(groupId, provider) {
            const keyStatusData =
                this.keyStatus.groups &&
                this.keyStatus.groups[groupId];

            if (keyStatusData) {
                // 如果有检测数据，使用检测结果
                const valid = keyStatusData.valid_keys || 0;
                const invalid = keyStatusData.invalid_keys || 0;
                const unknown = keyStatusData.unknown_keys || 0;
                const total = keyStatusData.total_keys || 0;

                return {
                    total: total,
                    valid: valid,
                    invalid: invalid,
                    unknown: unknown, // 使用来自后端的未检测密钥数量
                };
            } else {
                // 如果没有检测数据，使用服务商基本信息
                const total = provider.total_keys || 0;
                const available = provider.available_keys || 0;

                return {
                    total: total,
                    valid: 0, // 没有验证过的密钥不能确定为有效
                    invalid: 0, // 没有验证过的密钥不能确定为无效
                    unknown: total, // 所有密钥都是未检测状态
                };
            }
        },

        // 移除自动刷新相关方法
        // startAutoRefresh() {
        //     this.stopAutoRefresh();
        //     this.refreshTimer = setInterval(() => {
        //         this.refreshData();
        //     }, this.refreshInterval);
        // },

        // stopAutoRefresh() {
        //     if (this.refreshTimer) {
        //         clearInterval(this.refreshTimer);
        //         this.refreshTimer = null;
        //     }
        // },

        // updateRefreshInterval() {
        //     if (this.autoRefresh) {
        //         this.startAutoRefresh();
        //     }
        // },

        getStatusText(status) {
            const statusMap = {
                healthy: "健康",
                degraded: "降级",
                unhealthy: "异常",
            };
            return statusMap[status] || "未知";
        },

        getProviderTypeClass(type) {
            const typeClasses = {
                openai: "bg-blue-100 text-blue-800",
                gemini: "bg-green-100 text-green-800",
                anthropic: "bg-purple-100 text-purple-800",
                azure_openai: "bg-cyan-100 text-cyan-800",
            };
            return typeClasses[type] || "bg-gray-100 text-gray-800";
        },

        formatDuration(value) {
            if (!value) return "--";

            // 智能检测输入格式：如果值很大（>10^9），则认为是纳秒；否则认为是秒
            let seconds;
            if (value > 1000000000) {
                // 纳秒格式
                seconds = Math.floor(value / 1000000000);
            } else {
                // 秒格式
                seconds = Math.floor(value);
            }

            const days = Math.floor(seconds / 86400);
            const hours = Math.floor((seconds % 86400) / 3600);
            const minutes = Math.floor((seconds % 3600) / 60);

            if (days > 0) {
                return `${days}天 ${hours}小时 ${minutes}分钟`;
            } else if (hours > 0) {
                return `${hours}小时 ${minutes}分钟`;
            } else {
                return `${minutes}分钟`;
            }
        },

        formatResponseTime(nanoseconds) {
            if (!nanoseconds) return "--";
            const ms = Math.floor(nanoseconds / 1000000);
            return `${ms}ms`;
        },

        formatDate(dateStr) {
            if (!dateStr) return "--";
            return new Date(dateStr).toLocaleString("zh-CN");
        },


        // 启动系统健康状态的定时刷新（只刷新第一排系统信息）
        startSystemHealthRefresh() {
            // 立即执行一次刷新
            this.refreshSystemHealthOnly();

            // 设置定时器，每60秒刷新一次系统健康状态
            setInterval(() => {
                this.refreshSystemHealthOnly();
            }, 60000); // 60秒刷新一次
        },

        // 只刷新系统健康状态（第一排的系统信息）
        async refreshSystemHealthOnly() {
            try {
                await this.loadSystemHealth();
                this.lastUpdate = new Date();
            } catch (error) {
                console.error(
                    "Failed to refresh system health:",
                    error,
                );
            }
        },

        // 刷新所有数据（手动刷新时使用）
        async refreshSystemData() {
            try {
                await Promise.all([
                    this.loadSystemHealth(),
                    this.loadProviderStatuses(),
                ]);
                this.lastUpdate = new Date();
            } catch (error) {
                console.error(
                    "Failed to refresh system data:",
                    error,
                );
            }
        },

        // 刷新代理密钥使用次数（静默刷新，不显示加载状态）
        async refreshProxyKeysOnly() {
            try {
                // 如果代理密钥模态框打开，则刷新数据
                if (this.showProxyKeyModal) {
                    await this.loadProxyKeys();
                }
            } catch (error) {
                console.error(
                    "Failed to refresh proxy keys:",
                    error,
                );
            }
        },

        // 手动刷新所有分组的健康检查
        async refreshAllHealth() {
            this.loadingHealthRefresh = true;
            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch(
                    "/admin/health/refresh",
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            'Authorization': `Bearer ${token}`
                        },
                    },
                );

                const result = await response.json();

                if (response.ok) {
                    this.showMessage(
                        "健康检查已启动，请稍等片刻后查看结果",
                        "success",
                    );
                    // 延迟一段时间后刷新数据以显示检查结果
                    setTimeout(async () => {
                        await this.loadProviderStatuses();
                    }, 3000);
                } else {
                    throw new Error(
                        result.error || "刷新健康检查失败",
                    );
                }
            } catch (error) {
                console.error("刷新健康检查失败:", error);
                this.showMessage(
                    "刷新健康检查失败: " + error.message,
                    "error",
                );
            } finally {
                this.loadingHealthRefresh = false;
            }
        },

        // 代理密钥管理方法
        async loadProxyKeys(page = null, search = null, sortBy = null) {
            try {
                // 使用传入的参数或当前状态
                const currentPage =
                    page !== null ? page : this.proxyKeyPage;
                const currentSearch =
                    search !== null ? search : this.proxyKeySearch;
                const currentSortBy =
                    sortBy !== null ? sortBy : this.proxyKeySortBy;

                // 构建查询参数
                const params = new URLSearchParams({
                    page: currentPage.toString(),
                    page_size: this.proxyKeyPageSize.toString(),
                    sort_by: currentSortBy,
                });

                if (currentSearch.trim()) {
                    params.append("search", currentSearch.trim());
                }

                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch(
                    `/admin/proxy-keys?${params}`,
                    {
                        method: 'GET',
                        headers: {
                            'Authorization': `Bearer ${token}`,
                            'Content-Type': 'application/json'
                        }
                    }
                );
                const result = await response.json();

                if (result.success) {
                    this.proxyKeys = result.keys || [];
                    this.proxyKeyPagination =
                        result.pagination || {};
                    this.proxyKeyPage =
                        this.proxyKeyPagination.page || 1;
                    // 更新当前排序状态
                    if (result.sort_by) {
                        this.proxyKeySortBy = result.sort_by;
                    }
                } else {
                    console.error("加载代理密钥失败:", result);
                }
            } catch (error) {
                console.error("加载代理密钥失败:", error);
            }
        },

        async generateProxyKey() {
            if (!this.newProxyKey.name.trim()) {
                this.showMessage("请输入密钥名称", "error");
                return;
            }

            try {
                // 准备请求数据
                const requestData = {
                    name: this.newProxyKey.name,
                    description: this.newProxyKey.description,
                    allowed_groups: this.newProxyKey.allowed_groups,
                };

                // 如果有多个分组或空分组（访问所有分组），添加分组选择配置
                if (
                    this.newProxyKey.allowed_groups.length > 1 ||
                    this.newProxyKey.allowed_groups.length === 0
                ) {
                    // 转换前端驼峰命名为后端下划线命名
                    requestData.group_selection_config = {
                        strategy: this.newProxyKey.group_selection_config.strategy,
                        group_weights: this.newProxyKey.group_selection_config.group_weights
                    };
                }

                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch("/admin/proxy-keys", {
                    method: "POST",
                    headers: {
                        "Authorization": `Bearer ${token}`,
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify(requestData),
                });

                const result = await response.json();

                if (result.success) {
                    this.showMessage(
                        "代理密钥生成成功！",
                        "success",
                    );
                    this.showGenerateProxyKeyForm = false;
                    this.resetProxyKeyForm();
                    await this.loadProxyKeys();
                } else {
                    this.showMessage(
                        "生成失败: " + (result.error || "未知错误"),
                        "error",
                    );
                }
            } catch (error) {
                console.error("生成代理密钥失败:", error);
                this.showMessage("网络错误，请检查连接", "error");
            }
        },

        async deleteProxyKey(keyId) {
            if (
                !confirm(
                    "确定要删除这个代理密钥吗？删除后无法恢复。",
                )
            ) {
                return;
            }

            try {
                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch(
                    `/admin/proxy-keys/${keyId}`,
                    {
                        method: "DELETE",
                        headers: {
                            "Authorization": `Bearer ${token}`,
                            "Content-Type": "application/json",
                        },
                    },
                );

                const result = await response.json();
                if (result.success) {
                    this.showMessage(
                        "代理密钥删除成功！",
                        "success",
                    );
                    await this.loadProxyKeys();
                } else {
                    this.showMessage(
                        "删除失败: " + (result.error || "未知错误"),
                        "error",
                    );
                }
            } catch (error) {
                console.error("删除代理密钥失败:", error);
                this.showMessage("网络错误，请检查连接", "error");
            }
        },

        resetProxyKeyForm() {
            this.newProxyKey = {
                name: "",
                description: "",
                allowed_groups: [],
                group_selection_config: {
                    strategy: "round_robin",
                    group_weights: [],
                },
            };
        },

        toggleProxyKeyGroup(groupId, checked) {
            if (checked) {
                if (
                    !this.newProxyKey.allowed_groups.includes(
                        groupId,
                    )
                ) {
                    this.newProxyKey.allowed_groups.push(groupId);

                    // 如果当前策略是权重模式，为新添加的分组添加权重配置
                    if (this.newProxyKey.group_selection_config.strategy === "weighted") {
                        const existingWeight = this.newProxyKey.group_selection_config.group_weights.find(
                            (w) => w.group_id === groupId
                        );
                        if (!existingWeight) {
                            this.newProxyKey.group_selection_config.group_weights.push({
                                group_id: groupId,
                                weight: 1
                            });
                        }
                    }
                }
            } else {
                const index =
                    this.newProxyKey.allowed_groups.indexOf(groupId);
                if (index > -1) {
                    this.newProxyKey.allowed_groups.splice(index, 1);

                    // 移除对应的权重配置
                    const weightIndex = this.newProxyKey.group_selection_config.group_weights.findIndex(
                        (w) => w.group_id === groupId
                    );
                    if (weightIndex > -1) {
                        this.newProxyKey.group_selection_config.group_weights.splice(
                            weightIndex,
                            1
                        );
                    }
                }
            }
        },

        getGroupName(groupId) {
            const provider = this.providerStatuses[groupId];
            return provider ? provider.group_name : groupId;
        },



        // 编辑代理密钥相关方法
        editProxyKey(key) {

            this.editingProxyKey = {
                id: key.id,
                name: key.name,
                description: key.description || "",
                is_active: key.is_active !== false, // 默认为true
                allowed_groups: key.allowed_groups
                    ? [...key.allowed_groups]
                    : [],
                group_selection_config: key.group_selection_config
                    ? {
                        strategy:
                            key.group_selection_config.strategy ||
                            "round_robin",
                        group_weights: key.group_selection_config
                            .group_weights
                            ? [
                                ...key.group_selection_config
                                    .group_weights,
                            ]
                            : [],
                    }
                    : {
                        strategy: "round_robin",
                        group_weights: [],
                    },
            };

            // 如果当前策略是权重模式，确保所有允许的分组都有权重配置
            if (this.editingProxyKey.group_selection_config.strategy === "weighted") {
                this.ensureAllGroupsHaveWeights();
            }


            this.showEditProxyKeyModal = true;
        },

        closeEditProxyKeyModal() {
            this.showEditProxyKeyModal = false;
            this.editingProxyKey = {
                id: "",
                name: "",
                description: "",
                is_active: true,
                allowed_groups: [],
                group_selection_config: {
                    strategy: "round_robin",
                    group_weights: [],
                },
            };
        },

        toggleEditingProxyKeyGroup(groupId, checked) {
            if (checked) {
                if (
                    !this.editingProxyKey.allowed_groups.includes(
                        groupId,
                    )
                ) {
                    this.editingProxyKey.allowed_groups.push(
                        groupId,
                    );
                }
            } else {
                const index =
                    this.editingProxyKey.allowed_groups.indexOf(
                        groupId,
                    );
                if (index > -1) {
                    this.editingProxyKey.allowed_groups.splice(
                        index,
                        1,
                    );
                }
            }

            // 如果当前策略是权重模式，同步权重配置
            if (this.editingProxyKey.group_selection_config.strategy === "weighted") {
                this.ensureAllGroupsHaveWeights();
            }
        },

        async updateProxyKey() {
            if (!this.editingProxyKey.name.trim()) {
                this.showMessage("请输入密钥名称", "error");
                return;
            }

            try {
                // 如果当前策略是权重模式，确保所有分组都有权重配置
                if (this.editingProxyKey.group_selection_config.strategy === "weighted") {
                    this.ensureAllGroupsHaveWeights();
                }

                // 准备请求数据
                const requestData = {
                    name: this.editingProxyKey.name,
                    description: this.editingProxyKey.description,
                    is_active: this.editingProxyKey.is_active,
                    allowed_groups:
                        this.editingProxyKey.allowed_groups,
                };

                // 如果有多个分组或空分组（访问所有分组），添加分组选择配置
                if (
                    this.editingProxyKey.allowed_groups.length > 1 ||
                    this.editingProxyKey.allowed_groups.length === 0
                ) {
                    // 转换前端驼峰命名为后端下划线命名
                    requestData.group_selection_config = {
                        strategy: this.editingProxyKey.group_selection_config.strategy,
                        group_weights: this.editingProxyKey.group_selection_config.group_weights
                    };
                }



                const token = localStorage.getItem('authToken');
                if (!token) {
                    throw new Error("未找到认证令牌");
                }

                const response = await fetch(
                    `/admin/proxy-keys/${this.editingProxyKey.id}`,
                    {
                        method: "PUT",
                        headers: {
                            "Authorization": `Bearer ${token}`,
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify(requestData),
                    },
                );

                const result = await response.json();

                if (result.success) {
                    this.showMessage(
                        "代理密钥更新成功！",
                        "success",
                    );
                    this.closeEditProxyKeyModal();

                    // 重新加载数据以确保界面显示最新数据
                    await this.loadProxyKeys();
                } else {
                    this.showMessage(
                        "更新失败: " + (result.error || "未知错误"),
                        "error",
                    );
                }
            } catch (error) {
                console.error("更新代理密钥失败:", error);
                this.showMessage("网络错误，请检查连接", "error");
            }
        },

        copyToClipboard(text) {
            navigator.clipboard
                .writeText(text)
                .then(() => {
                    this.showMessage("已复制到剪贴板", "success");
                })
                .catch(() => {
                    // 降级方案
                    const textArea =
                        document.createElement("textarea");
                    textArea.value = text;
                    document.body.appendChild(textArea);
                    textArea.select();
                    document.execCommand("copy");
                    document.body.removeChild(textArea);
                    this.showMessage("已复制到剪贴板", "success");
                });
        },

        // 代理密钥搜索和分页方法
        async searchProxyKeys() {
            this.proxyKeyPage = 1; // 搜索时重置到第一页
            await this.loadProxyKeys(1, this.proxyKeySearch);
        },

        // 更改代理密钥排序方式
        async changeProxyKeySortBy() {
            this.proxyKeyPage = 1; // 重置到第一页
            await this.loadProxyKeys();
        },

        async goToProxyKeyPage(page) {
            if (
                page >= 1 &&
                page <= this.proxyKeyPagination.total_pages
            ) {
                await this.loadProxyKeys(page);
            }
        },

        async changeProxyKeyPageSize() {
            this.proxyKeyPage = 1; // 改变页面大小时重置到第一页
            await this.loadProxyKeys(1);
        },

        clearProxyKeySearch() {
            this.proxyKeySearch = "";
            this.searchProxyKeys();
        },

        viewProviderDetails(groupId) {
            // 实现查看服务商详情的逻辑
            alert(`查看服务商 ${groupId} 的详细信息`);
        },

        exportHealthReport() {
            // 实现导出健康报告的逻辑
            const report = {
                timestamp: new Date().toISOString(),
                system_health: this.systemHealth,
                provider_statuses: this.providerStatuses,
            };

            const blob = new Blob(
                [JSON.stringify(report, null, 2)],
                { type: "application/json" },
            );
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = `health_report_${new Date().toISOString().split("T")[0]}.json`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        },

        // 分组选择配置相关方法
        getGroupsForWeightConfig(allowed_groups) {
            let groups;
            // 如果没有选择任何分组，返回所有启用的分组
            if (allowed_groups.length === 0) {
                groups = Object.keys(this.providerStatuses).filter(
                    (groupId) =>
                        this.providerStatuses[groupId] &&
                        this.providerStatuses[groupId].enabled,
                );
            } else {
                // 否则返回选择的分组
                groups = allowed_groups;
            }
            
            // 按权重或优先级倒序排序（数值越大越靠前）
            return this.sortGroupsByWeightDesc(groups);
        },

        // 按权重倒序排序分组（数值越大越靠前）
        sortGroupsByWeightDesc(groups) {
            if (!groups || groups.length <= 1) {
                return groups;
            }

            // 获取当前策略和权重配置
            const strategy = this.getCurrentStrategy();
            const weights = this.getCurrentWeights();
            
            // 按权重倒序排序
            return groups.slice().sort((a, b) => {
                const weightA = this.getGroupWeightValue(a, weights);
                const weightB = this.getGroupWeightValue(b, weights);
                return weightB - weightA; // 倒序：数值越大越靠前
            });
        },

        // 获取当前策略
        getCurrentStrategy() {
            if (this.editingProxyKey && this.editingProxyKey.group_selection_config) {
                return this.editingProxyKey.group_selection_config.strategy;
            }
            if (this.newProxyKey && this.newProxyKey.group_selection_config) {
                return this.newProxyKey.group_selection_config.strategy;
            }
            return 'round_robin';
        },

        // 获取当前权重配置
        getCurrentWeights() {
            if (this.editingProxyKey && this.editingProxyKey.group_selection_config) {
                return this.editingProxyKey.group_selection_config.group_weights || [];
            }
            if (this.newProxyKey && this.newProxyKey.group_selection_config) {
                return this.newProxyKey.group_selection_config.group_weights || [];
            }
            return [];
        },

        // 获取分组的权重值
        getGroupWeightValue(groupId, weights) {
            const weight = weights.find(w => w.group_id === groupId);
            return weight ? (parseInt(weight.weight) || 1) : 1;
        },

        // 获取按权重倒序排序的分组权重列表
        getSortedGroupWeights(weights) {
            if (!weights || !Array.isArray(weights)) {
                return [];
            }
            return weights.slice().sort((a, b) => (b.weight || 1) - (a.weight || 1));
        },

        onNewProxyKeyStrategyChange() {
            const prevStrategy = this._newProxyKeyPrevStrategy || "round_robin";
            // 保存上一策略的权重快照
            this.newProxyKeyWeightsSnapshot[prevStrategy] = JSON.parse(JSON.stringify(this.newProxyKey.group_selection_config.group_weights || []));

            const currentStrategy = this.newProxyKey.group_selection_config.strategy;
            if (currentStrategy === "weighted" || currentStrategy === "failover") {
                // 为每个允许的分组初始化权重，优先从快照恢复
                const groups = this.getGroupsForWeightConfig(this.newProxyKey.allowed_groups);

                const currentWeights = this.newProxyKey.group_selection_config.group_weights || [];
                const validWeights = (this.newProxyKey.allowed_groups.length === 0)
                    ? currentWeights
                    : currentWeights.filter(w => this.newProxyKey.allowed_groups.includes(w.group_id));

                const snapshot = this.newProxyKeyWeightsSnapshot[currentStrategy] || [];

                const weights = groups.map((groupId) => {
                    const snap = snapshot.find(w => w.group_id === groupId);
                    if (snap) {
                        return { group_id: groupId, weight: parseInt(snap.weight) || 1 };
                    }
                    const existingWeight = validWeights.find(w => w.group_id === groupId);
                    return existingWeight || { group_id: groupId, weight: 1 };
                });
                
                // 按权重倒序排序（数值越大越靠前）
                this.newProxyKey.group_selection_config.group_weights = weights.sort((a, b) => b.weight - a.weight);
            }
            // 更新当前策略为“上一策略”
            this._newProxyKeyPrevStrategy = currentStrategy;
        },

        onEditingProxyKeyStrategyChange() {
            const prevStrategy = this._editingProxyKeyPrevStrategy || "round_robin";
            // 保存上一策略快照
            this.editingProxyKeyWeightsSnapshot[prevStrategy] = JSON.parse(JSON.stringify(this.editingProxyKey.group_selection_config.group_weights || []));

            const currentStrategy = this.editingProxyKey.group_selection_config.strategy;
            if (currentStrategy === "weighted" || currentStrategy === "failover") {
                const groups = this.getGroupsForWeightConfig(this.editingProxyKey.allowed_groups);

                const currentWeights = this.editingProxyKey.group_selection_config.group_weights || [];
                const validWeights = (this.editingProxyKey.allowed_groups.length === 0)
                    ? currentWeights
                    : currentWeights.filter(w => this.editingProxyKey.allowed_groups.includes(w.group_id));

                const snapshot = this.editingProxyKeyWeightsSnapshot[currentStrategy] || [];

                const weights = groups.map((groupId) => {
                    const snap = snapshot.find(w => w.group_id === groupId);
                    if (snap) {
                        return { group_id: groupId, weight: parseInt(snap.weight) || 1 };
                    }
                    const existingWeight = validWeights.find(w => w.group_id === groupId);
                    return existingWeight || { group_id: groupId, weight: 1 };
                });
                
                // 按权重倒序排序（数值越大越靠前）
                this.editingProxyKey.group_selection_config.group_weights = weights.sort((a, b) => b.weight - a.weight);
            }
            this._editingProxyKeyPrevStrategy = currentStrategy;
        },

        // 确保所有允许的分组都有权重配置
        ensureAllGroupsHaveWeights() {
            const groups = this.getGroupsForWeightConfig(
                this.editingProxyKey.allowed_groups,
            );

            // 清理不再属于允许分组的权重配置
            const currentWeights = this.editingProxyKey.group_selection_config.group_weights || [];
            const validWeights = (this.editingProxyKey.allowed_groups.length === 0)
                ? currentWeights // 未选择任何分组=全部分组，保留所有已有权重
                : currentWeights.filter(w => this.editingProxyKey.allowed_groups.includes(w.group_id));

            // 为缺少权重配置的分组添加默认权重，保留已有的权重值
            const weights = groups.map((groupId) => {
                const existingWeight = validWeights.find(w => w.group_id === groupId);
                if (existingWeight) {
                    // 返回现有的权重配置，保持用户设置的权重值
                    return existingWeight;
                } else {
                    // 只为新添加的分组设置默认权重1
                    return {
                        group_id: groupId,
                        weight: 1
                    };
                }
            });
            
            // 按权重倒序排序（数值越大越靠前）
            this.editingProxyKey.group_selection_config.group_weights = weights.sort((a, b) => b.weight - a.weight);
        },

        getGroupWeight(groupId) {
            const weight =
                this.newProxyKey.group_selection_config.group_weights.find(
                    (w) => w.group_id === groupId,
                );
            return weight ? weight.weight : 1;
        },

        setGroupWeight(groupId, weight) {
            const weightObj =
                this.newProxyKey.group_selection_config.group_weights.find(
                    (w) => w.group_id === groupId,
                );
            if (weightObj) {
                weightObj.weight = parseInt(weight) || 1;
            } else {
                this.newProxyKey.group_selection_config.group_weights.push(
                    {
                        group_id: groupId,
                        weight: parseInt(weight) || 1,
                    },
                );
            }
            
            // 重新按权重倒序排序（数值越大越靠前）
            this.newProxyKey.group_selection_config.group_weights.sort((a, b) => b.weight - a.weight);
            
            // 同步更新当前策略的快照
            const currentStrategy = this.newProxyKey.group_selection_config.strategy || "round_robin";
            this.newProxyKeyWeightsSnapshot[currentStrategy] = JSON.parse(JSON.stringify(this.newProxyKey.group_selection_config.group_weights || []));
        },

        getEditingGroupWeight(groupId) {
            const weight =
                this.editingProxyKey.group_selection_config.group_weights.find(
                    (w) => w.group_id === groupId,
                );
            return weight ? weight.weight : 1;
        },

        setEditingGroupWeight(groupId, weight) {
            const weightObj =
                this.editingProxyKey.group_selection_config.group_weights.find(
                    (w) => w.group_id === groupId,
                );
            if (weightObj) {
                weightObj.weight = parseInt(weight) || 1;
            } else {
                this.editingProxyKey.group_selection_config.group_weights.push(
                    {
                        group_id: groupId,
                        weight: parseInt(weight) || 1,
                    },
                );
            }
            
            // 重新按权重倒序排序（数值越大越靠前）
            this.editingProxyKey.group_selection_config.group_weights.sort((a, b) => b.weight - a.weight);
            
            // 同步更新当前策略的快照
            const currentStrategyEdit = this.editingProxyKey.group_selection_config.strategy || "round_robin";
            this.editingProxyKeyWeightsSnapshot[currentStrategyEdit] = JSON.parse(JSON.stringify(this.editingProxyKey.group_selection_config.group_weights || []));
        },

        getStrategyDisplayName(strategy) {
            const names = {
                round_robin: "轮询",
                weighted: "权重",
                random: "随机",
                failover: "故障转移",
            };
            return names[strategy] || strategy;
        },

        getStrategyBadgeClass(strategy) {
            const classes = {
                round_robin: "bg-blue-100 text-blue-800",
                weighted: "bg-purple-100 text-purple-800",
                random: "bg-green-100 text-green-800",
                failover: "bg-orange-100 text-orange-800",
            };
            return classes[strategy] || "bg-gray-100 text-gray-800";
        },

        async viewGroupStats(keyId) {
            try {
                const response = await fetch(
                    `/admin/proxy-keys/${keyId}/group-stats`,
                );
                const result = await response.json();

                if (result.success) {
                    // 显示统计信息的模态框或弹窗
                    let statsText = "分组使用统计:\n\n";
                    for (const [groupId, stats] of Object.entries(
                        result.stats,
                    )) {
                        statsText += `${this.getGroupName(groupId)}:\n`;
                        statsText += `  使用次数: ${stats.usage_count}\n`;
                        statsText += `  最后使用: ${stats.last_used ? this.formatDate(stats.last_used) : "从未使用"}\n\n`;
                    }
                    alert(statsText);
                } else {
                    this.showMessage(
                        "获取统计信息失败: " +
                        (result.error || "未知错误"),
                        "error",
                    );
                }
            } catch (error) {
                console.error("获取分组统计失败:", error);
                this.showMessage("网络错误，请检查连接", "error");
            }
        },

        viewSystemLogs() {
            window.location.href = "/logs";
        },

        // 密钥使用统计相关方法
        async showKeyUsageStats(groupId, provider) {
            this.loadingUsageStats = true;
            this.currentGroupUsageStats = null;
            this.showKeyUsageStatsModal = true;

            try {
                const response = await fetch(`/admin/groups/${groupId}/keys/usage-stats`);
                const result = await response.json();

                if (result.success) {
                    this.currentGroupUsageStats = result;
                } else {
                    this.showMessage('获取使用统计失败: ' + (result.error || '未知错误'), 'error');
                    this.showKeyUsageStatsModal = false;
                }
            } catch (error) {
                console.error('获取密钥使用统计失败:', error);
                this.showMessage('网络错误，请检查连接', 'error');
                this.showKeyUsageStatsModal = false;
            } finally {
                this.loadingUsageStats = false;
            }
        },

        closeKeyUsageStatsModal() {
            this.showKeyUsageStatsModal = false;
            this.currentGroupUsageStats = null;
            this.selectedForceUpdateKey = null;
        },

        async resetGroupUsageStats(groupId) {
            if (!confirm('确定要重置该分组的所有密钥使用统计吗？此操作不可恢复。')) {
                return;
            }

            try {
                const response = await fetch(`/admin/groups/${groupId}/keys/reset-stats`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': 'Bearer ' + localStorage.getItem('token')
                    }
                });

                const result = await response.json();

                if (result.success) {
                    this.showMessage('使用统计重置成功', 'success');
                    // 重新加载统计数据
                    await this.showKeyUsageStats(groupId);
                } else {
                    this.showMessage('重置失败: ' + (result.error || '未知错误'), 'error');
                }
            } catch (error) {
                console.error('重置使用统计失败:', error);
                this.showMessage('网络错误，请检查连接', 'error');
            }
        },

        openForceUpdateModal(keyStats) {
            this.selectedForceUpdateKey = {
                api_key_prefix: keyStats.api_key_prefix,
                api_key_hash: keyStats.api_key_hash,
                current_status: keyStats.is_available ? 'valid' : 'invalid'
            };
            this.forceUpdateStatus = keyStats.is_available ? 'invalid' : 'valid'; // 默认切换状态
            this.showForceUpdateModal = true;
        },

        closeForceUpdateModal() {
            this.showForceUpdateModal = false;
            this.selectedForceUpdateKey = null;
            this.forceUpdateStatus = 'valid';
        },

        async submitForceUpdate() {
            if (!this.selectedForceUpdateKey || !this.currentGroupUsageStats) {
                return;
            }

            // 使用密钥哈希进行更新，安全且不暴露完整密钥
            const requestData = {
                api_key: this.selectedForceUpdateKey.api_key_hash, // 使用哈希值而不是完整密钥
                status: this.forceUpdateStatus
            };

            try {
                const response = await fetch(`/admin/groups/${this.currentGroupUsageStats.group_id}/keys/force-status`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': 'Bearer ' + localStorage.getItem('token')
                    },
                    body: JSON.stringify(requestData)
                });

                const result = await response.json();

                if (result.success) {
                    this.showMessage('密钥状态更新成功', 'success');
                    this.closeForceUpdateModal();
                    // 重新加载统计数据
                    await this.showKeyUsageStats(this.currentGroupUsageStats.group_id);
                } else {
                    this.showMessage('更新失败: ' + (result.error || '未知错误'), 'error');
                }
            } catch (error) {
                console.error('强制更新密钥状态失败:', error);
                this.showMessage('网络错误，请检查连接', 'error');
            }
        },
    };
}

function logout() {
    const token = localStorage.getItem('authToken');

    // 清理本地存储
    localStorage.removeItem('authToken');

    if (token) {
        fetch("/auth/logout", {
            method: "POST",
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        })
            .then((response) => {
                window.location.href = "/login";
            })
            .catch((error) => {
                console.error("Logout error:", error);
                window.location.href = "/login";
            });
    } else {
        window.location.href = "/login";
    }
}
