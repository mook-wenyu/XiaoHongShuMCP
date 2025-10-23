/* 中文注释：参数验证错误测试 */
import { describe, it, expect } from "vitest";
import { ValidationError } from "../../../src/core/errors/ValidationError.js";

describe("ValidationError 错误路径测试", () => {
	describe("错误创建和序列化", () => {
		it("应该正确创建验证错误", () => {
			const error = new ValidationError("Invalid email format", {
				field: "email",
				value: "invalid-email",
				expected: "valid email address"
			});

			expect(error).toBeInstanceOf(Error);
			expect(error).toBeInstanceOf(ValidationError);
			expect(error.message).toBe("Invalid email format");
			expect(error.code).toBe("VALIDATION_ERROR");
			expect(error.retryable).toBe(false);
			expect(error.context).toEqual({
				field: "email",
				value: "invalid-email",
				expected: "valid email address"
			});
		});

		it("应该正确序列化为 JSON", () => {
			const error = new ValidationError("Required field missing", {
				field: "username",
				value: undefined,
				constraint: "required"
			});

			const json = error.toJSON();

			expect(json).toMatchObject({
				name: "ValidationError",
				code: "VALIDATION_ERROR",
				message: "Required field missing",
				retryable: false,
				context: {
					field: "username",
					value: undefined,
					constraint: "required"
				}
			});
			expect(json).toHaveProperty("stack");
		});

		it("应该包含错误堆栈跟踪", () => {
			const error = new ValidationError("Invalid input");

			expect(error.stack).toBeDefined();
			expect(error.stack).toContain("ValidationError");
			expect(error.stack).toContain("Invalid input");
		});
	});

	describe("字段验证", () => {
		it("应该验证字符串长度", () => {
			const error = new ValidationError("String too long", {
				field: "description",
				value: "x".repeat(1000),
				maxLength: 500,
				actualLength: 1000
			});

			expect(error.context?.field).toBe("description");
			expect(error.context?.maxLength).toBe(500);
			expect(error.context?.actualLength).toBe(1000);
		});

		it("应该验证数值范围", () => {
			const error = new ValidationError("Value out of range", {
				field: "age",
				value: 150,
				min: 0,
				max: 120
			});

			expect(error.context?.value).toBe(150);
			expect(error.context?.min).toBe(0);
			expect(error.context?.max).toBe(120);
		});

		it("应该验证枚举值", () => {
			const error = new ValidationError("Invalid enum value", {
				field: "status",
				value: "invalid",
				allowedValues: ["pending", "active", "inactive"]
			});

			expect(error.context?.value).toBe("invalid");
			expect(error.context?.allowedValues).toEqual(["pending", "active", "inactive"]);
		});

		it("应该验证必填字段", () => {
			const error = new ValidationError("Required field missing", {
				field: "email",
				value: null,
				constraint: "required"
			});

			expect(error.context?.field).toBe("email");
			expect(error.context?.value).toBeNull();
			expect(error.context?.constraint).toBe("required");
		});
	});

	describe("类型验证", () => {
		it("应该验证数据类型", () => {
			const error = new ValidationError("Type mismatch", {
				field: "count",
				value: "123",
				expected: "number",
				actual: "string"
			});

			expect(error.context?.expected).toBe("number");
			expect(error.context?.actual).toBe("string");
		});

		it("应该验证对象结构", () => {
			const error = new ValidationError("Invalid object structure", {
				field: "user",
				value: { name: "John" },
				expectedFields: ["id", "name", "email"],
				missingFields: ["id", "email"]
			});

			expect(error.context?.expectedFields).toEqual(["id", "name", "email"]);
			expect(error.context?.missingFields).toEqual(["id", "email"]);
		});

		it("应该验证数组类型", () => {
			const error = new ValidationError("Invalid array element", {
				field: "tags",
				value: ["tag1", 123, "tag3"],
				index: 1,
				expected: "string",
				actual: "number"
			});

			expect(error.context?.index).toBe(1);
			expect(error.context?.expected).toBe("string");
			expect(error.context?.actual).toBe("number");
		});
	});

	describe("格式验证", () => {
		it("应该验证 Email 格式", () => {
			const error = new ValidationError("Invalid email format", {
				field: "email",
				value: "not-an-email",
				pattern: "/^[^@]+@[^@]+\\.[^@]+$/"
			});

			expect(error.context?.value).toBe("not-an-email");
			expect(error.context?.pattern).toBeDefined();
		});

		it("应该验证 URL 格式", () => {
			const error = new ValidationError("Invalid URL", {
				field: "website",
				value: "not-a-url",
				expected: "valid URL starting with http:// or https://"
			});

			expect(error.context?.value).toBe("not-a-url");
		});

		it("应该验证日期格式", () => {
			const error = new ValidationError("Invalid date format", {
				field: "birthdate",
				value: "2023-13-45",
				expected: "ISO 8601 date format (YYYY-MM-DD)"
			});

			expect(error.context?.value).toBe("2023-13-45");
		});
	});

	describe("复杂验证场景", () => {
		it("应该验证多个字段错误", () => {
			const errors = [
				new ValidationError("Invalid email", { field: "email" }),
				new ValidationError("Password too short", { field: "password" }),
				new ValidationError("Age out of range", { field: "age" })
			];

			expect(errors).toHaveLength(3);
			expect(errors[0].context?.field).toBe("email");
			expect(errors[1].context?.field).toBe("password");
			expect(errors[2].context?.field).toBe("age");
		});

		it("应该验证嵌套字段", () => {
			const error = new ValidationError("Invalid nested field", {
				field: "user.address.zipCode",
				value: "12345",
				expected: "5-digit string",
				path: ["user", "address", "zipCode"]
			});

			expect(error.context?.field).toBe("user.address.zipCode");
			expect(error.context?.path).toEqual(["user", "address", "zipCode"]);
		});

		it("应该验证条件验证", () => {
			const error = new ValidationError("Conditional validation failed", {
				field: "shippingAddress",
				value: null,
				condition: "requireShipping === true",
				reason: "Shipping address required when requireShipping is true"
			});

			expect(error.context?.condition).toBe("requireShipping === true");
			expect(error.context?.reason).toBeDefined();
		});
	});

	describe("错误消息本地化", () => {
		it("应该支持中文错误消息", () => {
			const error = new ValidationError("电子邮件格式无效", {
				field: "email",
				value: "无效邮箱"
			});

			expect(error.message).toBe("电子邮件格式无效");
		});

		it("应该支持错误消息模板", () => {
			const field = "password";
			const minLength = 8;
			const error = new ValidationError(
				`字段 '${field}' 的长度必须至少为 ${minLength} 个字符`,
				{ field, minLength }
			);

			expect(error.message).toBe("字段 'password' 的长度必须至少为 8 个字符");
		});
	});

	describe("错误类型判断", () => {
		it("应该正确识别为不可重试错误", () => {
			const error = new ValidationError("Invalid input");

			expect(error.retryable).toBe(false);
		});

		it("应该与其他错误类型区分", () => {
			const valError = new ValidationError("Validation error");
			const genericError = new Error("Generic error");

			expect(valError).toBeInstanceOf(ValidationError);
			expect(genericError).not.toBeInstanceOf(ValidationError);
		});
	});

	describe("边界条件", () => {
		it("应该处理空上下文", () => {
			const error = new ValidationError("Validation failed");

			expect(error.context).toBeUndefined();
		});

		it("应该处理 undefined 和 null 值", () => {
			const error1 = new ValidationError("Undefined value", {
				field: "test",
				value: undefined
			});
			const error2 = new ValidationError("Null value", {
				field: "test",
				value: null
			});

			expect(error1.context?.value).toBeUndefined();
			expect(error2.context?.value).toBeNull();
		});

		it("应该处理特殊值类型", () => {
			const error = new ValidationError("Special values", {
				field: "test",
				nan: NaN,
				infinity: Infinity,
				negInfinity: -Infinity
			});

			const json = error.toJSON();
			expect(json.context).toBeDefined();
		});

		it("应该处理循环引用", () => {
			const context: any = { field: "test" };
			context.self = context;

			const error = new ValidationError("Circular reference", context);

			expect(() => error.toJSON()).not.toThrow();
		});
	});

	describe("错误传播", () => {
		it("应该支持 try-catch 捕获", () => {
			expect(() => {
				throw new ValidationError("Test error");
			}).toThrow(ValidationError);

			expect(() => {
				throw new ValidationError("Test error");
			}).toThrow("Test error");
		});

		it("应该支持 async 错误捕获", async () => {
			const failingFunc = async () => {
				throw new ValidationError("Async validation error");
			};

			await expect(failingFunc()).rejects.toThrow(ValidationError);
			await expect(failingFunc()).rejects.toThrow("Async validation error");
		});
	});
});
