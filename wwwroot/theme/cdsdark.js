ace.define('ace/theme/cdsdark', [
    'require', 'exports', 'module',
    'ace/lib/dom'
], function(require, exports, module) {
    var dom = require('ace/lib/dom');

    exports.isDark = true;
    exports.cssClass = 'ace-cds-dark';
    exports.cssText = `
.ace-cds-dark {
    color: #e5e7eb;
    background-color: #111827;
}

/* gutter 行号区 */
.ace-cds-dark .ace_gutter {
    background: #020617;
    color: #4b5563;
}
.ace-cds-dark .ace_gutter-active-line {
    background-color: #1f2933;
}

/* 右侧 print margin */
.ace-cds-dark .ace_print-margin {
    width: 1px;
    background: #1f2937;
}

/* 光标 / 当前行 / 选中 */
.ace-cds-dark .ace_cursor {
    color: #f9fafb;
}
.ace-cds-dark .ace_marker-layer .ace_selection {
    background: rgba(55, 65, 81, 0.9);
}
.ace-cds-dark .ace_marker-layer .ace_active-line {
    background: #1f2937;
}
.ace-cds-dark .ace_marker-layer .ace_selected-word {
    border: 1px solid rgba(129, 140, 248, 0.9);
}

/* 括号匹配 */
.ace-cds-dark .ace_marker-layer .ace_bracket {
    margin: -1px 0 0 -1px;
    border: 1px solid rgba(75, 85, 99, 0.9);
}

/* 注释 */
.ace-cds-dark .ace_comment {
    color: #6b7280;
    font-style: italic;
}
.ace-cds-dark .ace_keyword.ace_todo {
    color: #f97316;
    font-weight: bold;
}

/* 关键字 */
.ace-cds-dark .ace_keyword {
    color: #60a5fa;
    font-weight: 600;
}

/* 布尔 / 常量 */
.ace-cds-dark .ace_constant.ace_language {
    color: #22d3ee;
    font-weight: 500;
}

/* 数字 */
.ace-cds-dark .ace_constant.ace_numeric {
    color: #a78bfa;
}

/* 字符串 */
.ace-cds-dark .ace_string {
    color: #34d399;
}

/* section 名字 [main] */
.ace-cds-dark .ace_entity.ace_name.ace_section {
    color: #fb923c;
    font-weight: 600;
}

/* key = value 里的 key */
.ace-cds-dark .ace_variable.ace_parameter {
    color: #2dd4bf;
    font-weight: 500;
}

/* path: value 这种 support.type */
.ace-cds-dark .ace_support.ace_type {
    color: #93c5fd;
}

/* @directive */
.ace-cds-dark .ace_keyword.ace_control {
    color: #e879f9;
}

/* 运算符 / 标点 / 括号 */
.ace-cds-dark .ace_keyword.ace_operator {
    color: #9ca3af;
}
.ace-cds-dark .ace_punctuation {
    color: #6b7280;
}
.ace-cds-dark .ace_paren {
    color: #9ca3af;
}

/* 折叠小三角 */
.ace-cds-dark .ace_fold {
    background-color: #60a5fa;
    border-color: #111827;
}

/* 不可见字符 */
.ace-cds-dark .ace_invisible {
    color: #374151;
}
`;

    dom.importCssString(exports.cssText, exports.cssClass);
});
