// 健康检查报表页面逻辑
function healthReportDashboard() {
    return {
        loading: false,
        triggeringCheck: false,
        reportData: [],
        overview: {},
        selectedGroup: null,
        showGroupDetails: false,

        async init() {
            console.log('Health report init - checking localStorage...');
            console.log('authToken exists:', !!localStorage.getItem('authToken'));
            console.log('token exists:', !!localStorage.getItem('token'));
            console.log('All localStorage keys:', Object.keys(localStorage));

            // 等待一小段时间确保页面完全加载
            await new Promise(resolve => setTimeout(resolve, 500));

            // 检查认证状态
            const authToken = localStorage.getItem('authToken');
            if (!authToken) {
                console.warn('未找到认证token，尝试重新登录');
                window.location.href = '/login';
                return;
            }

            await this.loadHealthReport();
            await this.loadOverview();
        },

        async loadHealthReport() {
            this.loading = true;
            try {
                const authToken = localStorage.getItem('authToken');
                console.log('Loading health report with token:', authToken ? `${authToken.substring(0, 20)}...` : 'null');

                const response = await fetch('/admin/health-check/report', {
                    headers: {
                        'Authorization': `Bearer ${authToken}`
                    }
                });

                if (response.ok) {
                    const result = await response.json();
                    if (result.success) {
                        this.reportData = result.data || [];
                    } else {
                        console.error('获取健康检查报表失败:', result.error);
                        this.showNotification('获取健康检查报表失败: ' + result.error, 'error');
                    }
                } else {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
            } catch (error) {
                console.error('加载健康检查报表时发生错误:', error);
                this.showNotification('加载健康检查报表失败: ' + error.message, 'error');
            } finally {
                this.loading = false;
            }
        },

        async loadOverview() {
            try {
                const authToken = localStorage.getItem('authToken');
                console.log('Loading overview with token:', authToken ? `${authToken.substring(0, 20)}...` : 'null');

                const response = await fetch('/admin/health-check/overview', {
                    headers: {
                        'Authorization': `Bearer ${authToken}`
                    }
                });

                if (response.ok) {
                    const result = await response.json();
                    if (result.success) {
                        this.overview = result.data || {};
                    }
                }
            } catch (error) {
                console.error('加载健康检查概览时发生错误:', error);
            }
        },

        async triggerHealthCheck() {
            this.triggeringCheck = true;
            try {
                const authToken = localStorage.getItem('authToken');
                console.log('Triggering health check with token:', authToken ? `${authToken.substring(0, 20)}...` : 'null');

                const response = await fetch('/admin/health-check/trigger', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${authToken}`
                    },
                    body: JSON.stringify({})
                });

                if (response.ok) {
                    const result = await response.json();
                    if (result.success) {
                        this.showNotification(`健康检查完成: 总计 ${result.data.total_checks} 次检查，成功 ${result.data.successful_checks} 次，失败 ${result.data.failed_checks} 次`, 'success');
                        // 延迟刷新数据，让后台服务有时间更新统计
                        setTimeout(() => {
                            this.loadHealthReport();
                            this.loadOverview();
                        }, 2000);
                    } else {
                        this.showNotification('触发健康检查失败: ' + result.error, 'error');
                    }
                } else {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
            } catch (error) {
                console.error('触发健康检查时发生错误:', error);
                this.showNotification('触发健康检查失败: ' + error.message, 'error');
            } finally {
                this.triggeringCheck = false;
            }
        },

        async viewGroupDetails(groupId) {
            try {
                const response = await fetch(`/admin/health-check/group/${groupId}/details`, {
                    headers: {
                        'Authorization': `Bearer ${localStorage.getItem('authToken')}`
                    }
                });

                if (response.ok) {
                    const result = await response.json();
                    if (result.success) {
                        this.selectedGroup = result.data;
                        this.showGroupDetails = true;
                    } else {
                        this.showNotification('获取分组详情失败: ' + result.error, 'error');
                    }
                } else {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
            } catch (error) {
                console.error('获取分组详情时发生错误:', error);
                this.showNotification('获取分组详情失败: ' + error.message, 'error');
            }
        },

        async toggleGroupStatus(groupId, currentStatus) {
            const action = currentStatus ? '禁用' : '启用';
            if (!confirm(`确定要${action}这个服务商分组吗？`)) {
                return;
            }

            try {
                const response = await fetch(`/admin/groups/${groupId}/toggle`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${localStorage.getItem('authToken')}`
                    }
                });

                if (response.ok) {
                    const result = await response.json();
                    if (result.success) {
                        this.showNotification(`服务商分组已${action}`, 'success');
                        await this.loadHealthReport();
                    } else {
                        this.showNotification(`${action}服务商分组失败: ` + result.error, 'error');
                    }
                } else {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
            } catch (error) {
                console.error(`${action}服务商分组时发生错误:`, error);
                this.showNotification(`${action}服务商分组失败: ` + error.message, 'error');
            }
        },

        async deleteGroup(groupId) {
            if (!confirm('确定要删除这个服务商分组吗？此操作将同时删除相关的健康检查记录，且无法撤销！')) {
                return;
            }

            try {
                const response = await fetch(`/admin/groups/${groupId}`, {
                    method: 'DELETE',
                    headers: {
                        'Authorization': `Bearer ${localStorage.getItem('authToken')}`
                    }
                });

                if (response.ok) {
                    const result = await response.json();
                    if (result.success) {
                        this.showNotification('服务商分组已删除', 'success');
                        await this.loadHealthReport();
                        await this.loadOverview();
                    } else {
                        this.showNotification('删除服务商分组失败: ' + result.error, 'error');
                    }
                } else {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
            } catch (error) {
                console.error('删除服务商分组时发生错误:', error);
                this.showNotification('删除服务商分组失败: ' + error.message, 'error');
            }
        },

        getStatusClass(status) {
            switch (status) {
                case 'healthy':
                    return 'bg-green-100 text-green-800';
                case 'warning':
                    return 'bg-yellow-100 text-yellow-800';
                case 'unhealthy':
                    return 'bg-red-100 text-red-800';
                default:
                    return 'bg-gray-100 text-gray-800';
            }
        },

        getStatusText(status) {
            switch (status) {
                case 'healthy':
                    return '健康';
                case 'warning':
                    return '警告';
                case 'unhealthy':
                    return '异常';
                default:
                    return '未知';
            }
        },

        formatDateTime(dateTimeStr) {
            if (!dateTimeStr) return '未检查';
            
            try {
                const date = new Date(dateTimeStr);
                const now = new Date();
                const diffMs = now - date;
                const diffMins = Math.floor(diffMs / 60000);
                const diffHours = Math.floor(diffMins / 60);
                const diffDays = Math.floor(diffHours / 24);

                if (diffMins < 1) return '刚刚';
                if (diffMins < 60) return `${diffMins}分钟前`;
                if (diffHours < 24) return `${diffHours}小时前`;
                if (diffDays < 7) return `${diffDays}天前`;
                
                return date.toLocaleDateString('zh-CN', {
                    year: 'numeric',
                    month: '2-digit',
                    day: '2-digit',
                    hour: '2-digit',
                    minute: '2-digit'
                });
            } catch (error) {
                return dateTimeStr;
            }
        },

        showNotification(message, type = 'info') {
            // 创建通知元素
            const notification = document.createElement('div');
            notification.className = `fixed top-4 right-4 z-50 p-4 rounded-lg shadow-lg max-w-sm transition-all duration-300 transform translate-x-full`;
            
            // 根据类型设置样式
            switch (type) {
                case 'success':
                    notification.className += ' bg-green-500 text-white';
                    break;
                case 'error':
                    notification.className += ' bg-red-500 text-white';
                    break;
                case 'warning':
                    notification.className += ' bg-yellow-500 text-white';
                    break;
                default:
                    notification.className += ' bg-blue-500 text-white';
            }

            notification.innerHTML = `
                <div class="flex items-center justify-between">
                    <span class="text-sm font-medium">${message}</span>
                    <button onclick="this.parentElement.parentElement.remove()" class="ml-4 text-white hover:text-gray-200">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                        </svg>
                    </button>
                </div>
            `;

            document.body.appendChild(notification);

            // 显示动画
            setTimeout(() => {
                notification.classList.remove('translate-x-full');
            }, 100);

            // 自动隐藏
            setTimeout(() => {
                notification.classList.add('translate-x-full');
                setTimeout(() => {
                    if (notification.parentElement) {
                        notification.remove();
                    }
                }, 300);
            }, 5000);
        }
    };
}
