`mean-test.onnx` is a deterministic channel-mean test graph, not a CLIP model.
It accepts float32 RGB `[1,3,224,224]`, reduces axes 2 and 3 with `keepdims=0`,
and returns `[1,3]`. The graph uses ONNX opset 17 and IR version 9, with
`nibot.archive=clip-vision-v1` metadata. It tests preprocessing and real ONNX
Runtime invocation without downloading semantic model weights. No model generation
toolchain is required to build or test NiBot.
