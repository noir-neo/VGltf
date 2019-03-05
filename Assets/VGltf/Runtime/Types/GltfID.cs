//
// Copyright (c) 2019- yutopp (yutopp@gmail.com)
//
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at  https://www.boost.org/LICENSE_1_0.txt)
//

using VJson.Schema;

namespace VGltf.Types
{
    [JsonSchema(Minimum = 0)]
    public class GltfID : RefTag<int>
    {
    }
}
