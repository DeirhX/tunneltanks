#pragma once
#include <type_traits>

template <typename T, typename U>
concept same_as = std::is_same_v<T, U> && std::is_same_v<U, T>;

/* BasicVisitor: Basic visitor function, receiving visitable as its only argument, returning whether enumeration should continue  */
template<typename TVisitorFunc, typename TParam>
concept BasicVisitor = requires(TVisitorFunc visitor, TParam visited) { { visitor(visited) } -> same_as<bool>; };


