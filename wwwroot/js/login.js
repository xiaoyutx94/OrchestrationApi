// 密码显示/隐藏切换
document.getElementById('togglePassword').addEventListener('click', function () {
    const passwordInput = document.getElementById('password');
    const icon = this.querySelector('i');

    if (passwordInput.type === 'password') {
        passwordInput.type = 'text';
        icon.classList.remove('fa-eye');
        icon.classList.add('fa-eye-slash');
    } else {
        passwordInput.type = 'password';
        icon.classList.remove('fa-eye-slash');
        icon.classList.add('fa-eye');
    }
});

// 显示错误消息
function showError(message) {
    const errorDiv = document.getElementById('errorMessage');
    const errorText = document.getElementById('errorText');
    errorText.textContent = message;
    errorDiv.classList.remove('hidden');
}

// 隐藏错误消息
function hideError() {
    document.getElementById('errorMessage').classList.add('hidden');
}

// 设置加载状态
function setLoading(loading) {
    const button = document.getElementById('loginButton');
    const buttonText = document.getElementById('loginButtonText');
    const buttonLoading = document.getElementById('loginButtonLoading');

    if (loading) {
        button.disabled = true;
        buttonText.classList.add('hidden');
        buttonLoading.classList.remove('hidden');
    } else {
        button.disabled = false;
        buttonText.classList.remove('hidden');
        buttonLoading.classList.add('hidden');
    }
}

// 登录表单提交
document.getElementById('loginForm').addEventListener('submit', async function (e) {
    e.preventDefault();

    hideError();
    setLoading(true);

    const username = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value;

    if (!username || !password) {
        showError('请输入用户名和密码');
        setLoading(false);
        return;
    }

    try {
        const response = await fetch('/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ username, password })
        });

        const data = await response.json();
        console.log('Login API response data:', data);

        if (response.ok && data.success) {
            // 保存token
            localStorage.setItem('authToken', data.token);
            localStorage.setItem('tokenExpires', data.expiresAt);

            // 稍微延迟一下再跳转，确保Cookie设置完成
            setTimeout(() => {
                window.location.href = '/dashboard';
            }, 100);
        } else {
            showError(data.message || `登录失败: ${response.status}`);
        }
    } catch (error) {
        console.error('登录错误:', error);
        showError('网络错误，请稍后重试');
    } finally {
        setLoading(false);
    }
});

// 检查是否已登录
window.addEventListener('load', function () {
    const token = localStorage.getItem('authToken');
    const expires = localStorage.getItem('tokenExpires');

    if (token && expires && new Date(expires) > new Date()) {
        // 验证token是否有效
        fetch('/auth/verify', {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        })
            .then(response => {
                if (response.ok) return response.json();
                throw new Error('Token verification failed');
            })
            .then(data => {
                if (data.valid) {
                    window.location.href = '/dashboard';
                } else {
                    localStorage.removeItem('authToken');
                    localStorage.removeItem('tokenExpires');
                }
            })
            .catch(() => {
                // token无效，清除本地存储
                localStorage.removeItem('authToken');
                localStorage.removeItem('tokenExpires');
            });
    }
});