# 第三方组件与资源说明

本候选沿用项目现有依赖，不增加推理栈或新的UI框架。

* GPT-SoVITS：原交付Runtime的LICENSE已复制为GPT-SoVITS-LICENSE.txt；其余依赖原有许可证、Python包dist-info与原生库 notices保留在Runtime内部。模型和参考音频按本项目已有授权使用。
* Torch、Transformers、NumPy、SciPy、FastAPI、Uvicorn等：许可证与版本见Runtime/GPT-SoVITS/runtime/Lib/site-packages中的对应dist-info及随库文件。CUDA/cuDNN等原生依赖继续保留原有许可文件，本轮不宣称拥有其著作权。
* Spine Runtimes：见Spine-Runtimes-License.txt。用户已确认具备本项目使用授权；这不代表向他人授予Spine编辑器授权。
* Noto Sans CJK：见NotoSansCJK-OFL.txt及NotoSansCJK-Notice.txt。
* Unity Player/Mono、uGUI/TMP、UniWindowController及SQLite：按项目随包组件及各自许可使用；SQLite为public domain，工程源依赖说明见Packages与Assets内原始文档。
* Mika角色、美术、BGM、CHDDD与语音训练素材由用户提供，按项目原有非商用范围使用。本技术目录不是这些素材的再授权声明。

请保留整个Licenses目录及Runtime内原有许可文件；分发范围需要与实际已有素材/软件许可一致。
