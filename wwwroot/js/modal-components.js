/**
 * 通用模态框组件
 * 提供确认提示框和消息提示框功能
 * 基于Tailwind CSS样式，支持Alpine.js
 */

// 存储当前活动的模态框状态
let activeModal = null;

// 清理模态框事件监听器
function cleanupModalEvents(modal, handlers) {
    if (handlers) {
        handlers.forEach(handler => {
            if (handler.element && handler.event && handler.callback) {
                handler.element.removeEventListener(handler.event, handler.callback);
            }
        });
    }
}

// 自定义确认框函数
function showConfirm(message, title = '确认操作') {
    return new Promise((resolve) => {
        // 如果已有活动模态框，先关闭它
        if (activeModal) {
            activeModal.modal.classList.add('hidden');
            cleanupModalEvents(activeModal.modal, activeModal.handlers);
        }

        const modal = document.getElementById('confirmModal');
        const messageEl = document.getElementById('confirmMessage');
        const titleEl = modal.querySelector('h3');
        const yesBtn = document.getElementById('confirmYes');
        const noBtn = document.getElementById('confirmNo');
        const modalContent = modal.querySelector('.modal-content');

        // 检查元素是否存在
        if (!modal || !messageEl || !titleEl || !yesBtn || !noBtn || !modalContent) {
            console.error('Modal elements not found');
            resolve(false);
            return;
        }

        // 设置内容
        messageEl.textContent = message;
        titleEl.textContent = title;

        // 重置动画状态
        modalContent.style.transform = 'scale(0.95)';
        modalContent.style.opacity = '0';

        // 显示模态框
        modal.classList.remove('hidden');

        // 添加淡入动画
        requestAnimationFrame(() => {
            setTimeout(() => {
                modalContent.style.transform = 'scale(1)';
                modalContent.style.opacity = '1';
            }, 10);
        });

        // 事件处理器数组
        const handlers = [];

        const handleYes = () => {
            closeModal(true);
        };

        const handleNo = () => {
            closeModal(false);
        };

        const handleKeydown = (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                handleYes();
            } else if (e.key === 'Escape') {
                e.preventDefault();
                handleNo();
            }
        };

        const handleOverlayClick = (e) => {
            if (e.target === modal) {
                handleNo();
            }
        };

        const closeModal = (result) => {
            // 添加关闭动画
            modalContent.style.transform = 'scale(0.95)';
            modalContent.style.opacity = '0';

            setTimeout(() => {
                modal.classList.add('hidden');
                cleanupModalEvents(modal, handlers);
                activeModal = null;
                resolve(result);
            }, 200);
        };

        // 绑定事件监听器
        yesBtn.addEventListener('click', handleYes);
        noBtn.addEventListener('click', handleNo);
        document.addEventListener('keydown', handleKeydown);
        modal.addEventListener('click', handleOverlayClick);

        // 记录事件处理器
        handlers.push(
            { element: yesBtn, event: 'click', callback: handleYes },
            { element: noBtn, event: 'click', callback: handleNo },
            { element: document, event: 'keydown', callback: handleKeydown },
            { element: modal, event: 'click', callback: handleOverlayClick }
        );

        // 设置当前活动模态框
        activeModal = { modal, handlers };

        // 聚焦到取消按钮（更安全的默认选择）
        setTimeout(() => {
            noBtn.focus();
        }, 100);
    });
}

// 自定义提示框函数
function showAlert(message, type = 'info', title = '提示') {
    return new Promise((resolve) => {
        // 如果已有活动模态框，先关闭它
        if (activeModal) {
            activeModal.modal.classList.add('hidden');
            cleanupModalEvents(activeModal.modal, activeModal.handlers);
        }

        const modal = document.getElementById('alertModal');
        const messageEl = document.getElementById('alertMessage');
        const titleEl = document.getElementById('alertTitle');
        const iconEl = document.getElementById('alertIcon');
        const okBtn = document.getElementById('alertOk');
        const modalContent = modal.querySelector('.modal-content');

        // 检查元素是否存在
        if (!modal || !messageEl || !titleEl || !iconEl || !okBtn || !modalContent) {
            console.error('Alert modal elements not found');
            resolve();
            return;
        }

        // 设置内容
        messageEl.textContent = message;
        titleEl.textContent = title;

        // 根据类型设置图标和颜色
        let iconHTML = '';
        let buttonClass = 'modal-button px-4 py-2 text-white text-base font-medium rounded-md w-20 focus:outline-none transition-colors bg-blue-500 hover:bg-blue-600 focus:ring-blue-300';

        switch (type) {
            case 'success':
                iconHTML = `
                    <div class="bg-green-100 rounded-full p-3">
                        <svg class="h-6 w-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                        </svg>
                    </div>`;
                buttonClass = 'modal-button px-4 py-2 text-white text-base font-medium rounded-md w-20 focus:outline-none transition-colors bg-green-500 hover:bg-green-600 focus:ring-green-300';
                break;
            case 'error':
                iconHTML = `
                    <div class="bg-red-100 rounded-full p-3">
                        <svg class="h-6 w-6 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                        </svg>
                    </div>`;
                buttonClass = 'modal-button px-4 py-2 text-white text-base font-medium rounded-md w-20 focus:outline-none transition-colors bg-red-500 hover:bg-red-600 focus:ring-red-300';
                break;
            case 'warning':
                iconHTML = `
                    <div class="bg-yellow-100 rounded-full p-3">
                        <svg class="h-6 w-6 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16c-.77.833.192 2.5 1.732 2.5z"></path>
                        </svg>
                    </div>`;
                buttonClass = 'modal-button px-4 py-2 text-white text-base font-medium rounded-md w-20 focus:outline-none transition-colors bg-yellow-500 hover:bg-yellow-600 focus:ring-yellow-300';
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
        okBtn.className = buttonClass;

        // 重置动画状态
        modalContent.style.transform = 'scale(0.95)';
        modalContent.style.opacity = '0';

        // 显示模态框
        modal.classList.remove('hidden');

        // 添加淡入动画
        requestAnimationFrame(() => {
            setTimeout(() => {
                modalContent.style.transform = 'scale(1)';
                modalContent.style.opacity = '1';
            }, 10);
        });

        // 事件处理器数组
        const handlers = [];

        const handleOk = () => {
            closeModal();
        };

        const handleKeydown = (e) => {
            if (e.key === 'Enter' || e.key === 'Escape') {
                e.preventDefault();
                handleOk();
            }
        };

        const handleOverlayClick = (e) => {
            if (e.target === modal) {
                handleOk();
            }
        };

        const closeModal = () => {
            // 添加关闭动画
            modalContent.style.transform = 'scale(0.95)';
            modalContent.style.opacity = '0';

            setTimeout(() => {
                modal.classList.add('hidden');
                cleanupModalEvents(modal, handlers);
                activeModal = null;
                resolve();
            }, 200);
        };

        // 绑定事件监听器
        okBtn.addEventListener('click', handleOk);
        document.addEventListener('keydown', handleKeydown);
        modal.addEventListener('click', handleOverlayClick);

        // 记录事件处理器
        handlers.push(
            { element: okBtn, event: 'click', callback: handleOk },
            { element: document, event: 'keydown', callback: handleKeydown },
            { element: modal, event: 'click', callback: handleOverlayClick }
        );

        // 设置当前活动模态框
        activeModal = { modal, handlers };

        // 聚焦到确定按钮
        setTimeout(() => {
            okBtn.focus();
        }, 100);
    });
}

// 添加调试功能
function debugModal(message) {
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        console.log('[Modal Debug]', message);
    }
}

// 页面加载完成后检查模态框元素
document.addEventListener('DOMContentLoaded', function() {
    const confirmModal = document.getElementById('confirmModal');
    const alertModal = document.getElementById('alertModal');

    if (!confirmModal) {
        console.error('Confirm modal element not found! Make sure #confirmModal exists in the HTML.');
    } else {
        debugModal('Confirm modal element found');
    }

    if (!alertModal) {
        console.error('Alert modal element not found! Make sure #alertModal exists in the HTML.');
    } else {
        debugModal('Alert modal element found');
    }
});

// 导出函数供全局使用
window.showConfirm = showConfirm;
window.showAlert = showAlert;
window.debugModal = debugModal;
